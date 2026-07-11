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
        private static readonly int WalkHash = Animator.StringToHash("IsWalking");
        private static readonly int WorkHash = Animator.StringToHash("IsWorking");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
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
            _animator.SetBool(WalkHash, moving);
            _animator.SetBool(WorkHash, IsBusy && !moving);

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
