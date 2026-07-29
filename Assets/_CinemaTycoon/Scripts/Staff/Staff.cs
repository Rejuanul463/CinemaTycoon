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
        private float _initialWorkDuration;
        private bool _hasDecalTarget;
        private float _taskAssignedAt;
        private float _patrolTimer;
        private const float UnreachableTimeoutSeconds = 0.5f;

        private static readonly int WalkHash = Animator.StringToHash("isWalking");
        private static readonly int WorkHash = Animator.StringToHash("isWorking");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
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
            bool moving = _agent.velocity.sqrMagnitude > 0.01f;
            if (_animator != null)
            {
                _animator.SetBool(WalkHash, moving);
                _animator.SetBool(WorkHash, IsBusy && !moving);
            }

            if (_currentTask != null && !IsBusy)
            {
                if (_hasDecalTarget && _currentTask.TargetDecal == null)
                {
                    Debug.Log($"[Staff:{Role}] Task decal destroyed mid-task; returning home.");
                    _currentTask = null;
                    ReturnHome();
                    return;
                }

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

                if (!_agent.pathPending
                    && _agent.hasPath
                    && _agent.remainingDistance < 1.0f)
                {
                    BeginWork();
                }
            }
            else if (IsBusy)
            {
                TickDecalProgress();

                _workTimer -= Time.deltaTime * workSpeed;
                if (_workTimer <= 0f) CompleteTask();
            }
            else if (Role == StaffRole.Guard && _currentTask == null)
            {
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
            _hasDecalTarget = task != null && task.TargetDecal != null;
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
