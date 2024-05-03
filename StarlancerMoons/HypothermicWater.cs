using GameNetcodeStuff;
using UnityEngine;

namespace StarlancerMoons
{
    public class DamageTrigger : MonoBehaviour
    {
        public float cooldownTime;
        public int damageAmount;
        public CauseOfDeath causeOfDeath;
        private float timeSincePlayerDamaged = 0f;

        private void OnTriggerStay(Collider other)
        {
            PlayerControllerB victim = other.gameObject.GetComponent<PlayerControllerB>();
            if (!other.gameObject.CompareTag("Player"))
            {
                return;
            }
            if ((timeSincePlayerDamaged < cooldownTime))
            {
                timeSincePlayerDamaged += Time.deltaTime;
                return;
            }
            if (victim != null)
            {
                timeSincePlayerDamaged = 0f;
                victim.DamagePlayer(damageAmount, hasDamageSFX: true, callRPC: true, causeOfDeath);
            }
        }
    }
}
