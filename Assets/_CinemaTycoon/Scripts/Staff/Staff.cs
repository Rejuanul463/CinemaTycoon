using UnityEngine;
using UnityEngine.AI;
using CinemaTycoon.Events;
using CinemaTycoon.Core;

namespace CinemaTycoon.Staff
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class Staff : MonoBehaviour
    {
        [SerializeField] private float workSpeed = 1f;
        [SerializeField] private float patrolInterval = 12f;

        public StaffRole Role { get; private set; }
        public StaffRoleData Config { get; private set; }
        public bool IsBusy { get; private set; }
        public Vector3 HomeStation { get; private set; }

        private NavMeshAgent _agent;
        private Animator _animator;
        private StaffTask _currentTask;
        private float _workTimer;
        private float _initialWorkDuration;  // captured at BeginWork so progress is 0→1 across the timer
        // True only when the current task has a non-null TargetDecal at assignment
        // time. We need this to differentiate "decals-enabled task" from
        // "position-only task" — otherwise a task with no decal would falsely
        // "cancel" on the first frame (TargetDecal would always be null).
        private bool _hasDecalTarget;
        // Time.time at the last AssignTask call. Used to give the NavMeshAgent
        // a brief grace window to compute its path before we declare the
        // destination unreachable (pathPending → false happens within ~1 frame
        // for reachable points; we wait 0.5s so the agent has time to fail
        // out cleanly on unreachable ones too).
        private float _taskAssignedAt;
        private float _patrolTimer;
        private const float UnreachableTimeoutSeconds = 0.5f;

        // Cached Animator parameter hashes — avoids string lookups per frame.
        // Names MUST match the AnimatorController parameters exactly (case-sensitive).
        // WorkerAnimator.controller exposes "isWalking" / "isWorking" (lowercase).
        private static readonly int WalkHash = Animator.StringToHash("isWalking");
        private static readonly int WorkHash = Animator.StringToHash("isWorking");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            // NavMeshAgent drives the transform position; let the animator only
            // play clips in place. Leaving root motion on makes the agent and the
            // animation fight for the transform (drift / sliding).
            if (_animator != null) _animator.applyRootMotion = false;
        }

        public void Initialize(StaffRole role, StaffRoleData config, Vector3 station)
        {
            Role = role;
            Config = config;
            HomeStation = station;
            ReturnHome();
        }

        private void Update()
        {
            // Drive animator parameters from real state.
            bool moving = _agent.velocity.sqrMagnitude > 0.01f;
            if (_animator != null)
            {
                _animator.SetBool(WalkHash, moving);
                _animator.SetBool(WorkHash, IsBusy && !moving);
            }

            if (_currentTask != null && !IsBusy)
            {
                // Cancellation: the task had a decal target (e.g. a SpillDecal)
                // but it was destroyed before we arrived. Most common cause is
                // the spill expiring on the EventManager timer — in that case
                // there's nothing left to clean, so drop the task and go home
                // instead of playing the Work animation over empty floor.
                if (_hasDecalTarget && _currentTask.TargetDecal == null)
                {
                    Debug.Log($"[Staff:{Role}] Task decal destroyed mid-task; returning home.");
                    _currentTask = null;
                    ReturnHome();
                    return;
                }

                // Unreachable destination: the agent has finished computing its
                // path and has no valid path to the target. Without this guard
                // the agent would silently "arrive" (remainingDistance == 0) at
                // its current position and start working nowhere near the decal.
                // We give it a short grace period first (UnreachableTimeoutSeconds)
                // so a slow path-computation frame doesn't false-trigger.
                if (!_agent.pathPending
                    && !_agent.hasPath
                    && !_agent.isStopped
                    && Time.time - _taskAssignedAt > UnreachableTimeoutSeconds)
                {
                    Debug.LogWarning($"[Staff:{Role}] Cannot reach task target {_currentTask.TargetPosition} " +
                                     $"(no NavMesh path). Returning home. " +
                                     $"If this is a spill, make sure the 'Possible Spill Locations' " +
                                     $"are placed on the baked NavMesh.");
                    _currentTask = null;
                    ReturnHome();
                    return;
                }

                // Arrived at task target? Begin work. hasPath is critical here:
                // a destination on the NavMesh produces a valid path, while an
                // off-mesh one yields remainingDistance = 0 with hasPath = false.
                if (!_agent.pathPending
                    && _agent.hasPath
                    && _agent.remainingDistance < 1.0f)
                {
                    BeginWork();
                }
            }
            else if (IsBusy)
            {
                // Drive the decal's visual cleanup progress so the spill visibly
                // shrinks under the janitor's feet. No-op for tasks without a
                // decal (e.g. a position-only "patrol" task).
                TickDecalProgress();

                _workTimer -= Time.deltaTime * workSpeed;
                if (_workTimer <= 0f) CompleteTask();
            }
            else if (Role == StaffRole.Guard && _currentTask == null)
            {
                // Idle patrol for Guard: periodically pick a new patrol waypoint
                _patrolTimer += Time.deltaTime;
                if (_patrolTimer >= patrolInterval)
                {
                    _patrolTimer = 0f;
                    var wp = CinemaTycoon.Core.CinemaWaypoints.Instance;
                    var target = wp != null ? wp.PickRandomPatrolPoint() : null;
                    if (target != null)
                    {
                        _agent.isStopped = false;
                        _agent.SetDestination(target.position);
                    }
                }
            }
        }

        public void AssignTask(StaffTask task)
        {
            _currentTask = task;
            // Snapshot whether THIS task has a decal target. If yes, the Update
            // loop will watch for it being destroyed; if no, the task is
            // position-only and cannot be implicitly cancelled.
            _hasDecalTarget = task != null && task.TargetDecal != null;
            // Mark when the task was assigned so the unreachable-destination
            // check in Update can wait a grace period before declaring the
            // path invalid (avoids false positives on the first frame).
            _taskAssignedAt = Time.time;
            _agent.isStopped = false;
            _agent.SetDestination(task.TargetPosition);
            Debug.Log($"[Staff:{Role}] Assigned task → {task.TargetPosition} (decal: {(task.TargetDecal != null ? task.TargetDecal.name : "<none>")})");
        }

        public void ReturnHome()
        {
            _currentTask = null;
            _hasDecalTarget = false;
            _agent.isStopped = false;
            _agent.SetDestination(HomeStation);
        }

        private void BeginWork()
        {
            IsBusy = true;
            _initialWorkDuration = Config != null ? Config.taskDuration : 4f;
            _workTimer = _initialWorkDuration;
            _agent.isStopped = true;

            // Tell the decal (if any) that cleaning is starting. The decal
            // responds by stopping its own fade-in and preparing for the
            // shrink animation driven from TickDecalProgress.
            if (_hasDecalTarget
                && _currentTask.TargetDecal != null
                && _currentTask.TargetDecal.TryGetComponent(out SpillDecal decal))
            {
                decal.BeginCleaning();
            }
        }

        private void TickDecalProgress()
        {
            if (!_hasDecalTarget) return;
            if (_currentTask == null || _currentTask.TargetDecal == null) return;
            if (!_currentTask.TargetDecal.TryGetComponent(out SpillDecal decal)) return;

            // 0..1 progress through the work timer; drives the decal's
            // scale-down + alpha-fade so the player sees the spill being
            // mopped away in real time instead of popping out at the end.
            float progress = 1f - Mathf.Clamp01(_workTimer / Mathf.Max(0.001f, _initialWorkDuration));
            decal.SetCleaningProgress(progress);
        }

        private void CompleteTask()
        {
            IsBusy = false;
            _agent.isStopped = false;
            _currentTask?.OnComplete?.Invoke();
            _currentTask = null;
            _hasDecalTarget = false;
            ReturnHome();
        }
    }
}
