using UnityEngine;
using UnityEngine.AI;

namespace CinemaTycoon.Staff
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class Staff : MonoBehaviour
    {
        [SerializeField] private float workSpeed = 1f;

        public StaffRole Role { get; private set; }
        public StaffRoleData Config { get; private set; }
        public bool IsBusy { get; private set; }
        public Vector3 HomeStation { get; private set; }

        private NavMeshAgent _agent;
        private Animator _animator;
        private StaffTask _currentTask;
        private float _workTimer;

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
                // Arrived at task target? Begin work.
                if (!_agent.pathPending && _agent.remainingDistance < 1.0f)
                    BeginWork();
            }
            else if (IsBusy)
            {
                _workTimer -= Time.deltaTime * workSpeed;
                if (_workTimer <= 0f) CompleteTask();
            }
        }

        public void AssignTask(StaffTask task)
        {
            _currentTask = task;
            _agent.isStopped = false;
            _agent.SetDestination(task.TargetPosition);
        }

        public void ReturnHome()
        {
            _currentTask = null;
            _agent.isStopped = false;
            _agent.SetDestination(HomeStation);
        }

        private void BeginWork()
        {
            IsBusy = true;
            _workTimer = Config != null ? Config.taskDuration : 4f;
            _agent.isStopped = true;
        }

        private void CompleteTask()
        {
            IsBusy = false;
            _agent.isStopped = false;
            _currentTask?.OnComplete?.Invoke();
            _currentTask = null;
            ReturnHome();
        }
    }
}
