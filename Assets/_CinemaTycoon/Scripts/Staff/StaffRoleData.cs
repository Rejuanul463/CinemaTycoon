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
        public System.Action OnComplete;
        public int Priority;
    }
}
