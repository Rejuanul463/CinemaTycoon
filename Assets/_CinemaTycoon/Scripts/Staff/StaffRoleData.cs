using UnityEngine;

namespace CinemaTycoon.Staff
{
    public enum StaffRole { Cashier, Janitor, Guard }

    [CreateAssetMenu(fileName = "StaffRoleData", menuName = "CinemaTycoon/StaffRole")]
    public class StaffRoleData : ScriptableObject
    {
        public StaffRole role;
        public string displayName;
        public float hireCost = 100f;
        public float wagePerTick = 5f;
        [Tooltip("Seconds of in-game time to complete one assigned task.")]
        public float taskDuration = 4f;
    }

    /// <summary>
    /// Lightweight POCO task envelope. Priority is informational — StaffManager
    /// currently processes FIFO, but the field is here for future expansion.
    /// </summary>
    public class StaffTask
    {
        public Vector3 TargetPosition;
        /// <summary>
        /// Optional physical target the task is interacting with (e.g. a
        /// <see cref="CinemaTycoon.Events.SpillDecal"/> for janitor cleanup).
        /// When non-null, the staff member ties their work progress to the
        /// target's lifetime: if the GameObject is destroyed before the task
        /// completes (e.g. the spill expired), the task auto-cancels and the
        /// staff returns home instead of playing the Work animation over empty
        /// floor. Leave null for position-only tasks (no implicit cancel).
        /// </summary>
        public GameObject TargetDecal;
        public System.Action OnComplete;
        public int Priority;
        /// <summary>
        /// Optional role gate. When set, only idle staff whose <c>Role</c>
        /// matches will be considered for this task; other idle staff are
        /// ignored even if they are closer. Null = any role is acceptable.
        /// </summary>
        public StaffRole? RequiredRole;
    }
}
