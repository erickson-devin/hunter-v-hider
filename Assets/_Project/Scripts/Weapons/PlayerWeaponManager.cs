using UnityEngine;

namespace HunterVsHider.Weapons
{
    public class PlayerWeaponManager : MonoBehaviour
    {
        [Header("Equipped Weapon")]
        public Gun currentGun;

        private void Update()
        {
            if (currentGun == null) return;

            // Fire1 is typically Left Mouse Button
            if (Input.GetButton("Fire1"))
            {
                currentGun.TryFire();
            }
        }
    }
}
