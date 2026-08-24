using UnityEngine;
using Unity.Netcode;
using HunterVsHider.Weapons;
using HunterVsHider.Vision;

namespace HunterVsHider.Player
{
    public class PlayerWeaponManager : NetworkBehaviour
    {
        [Header("Equipped Weapon Slots (0: Primary/Rifle, 1: Shotgun, 2: Pistol)")]
        public Weapon[] weaponSlots = new Weapon[3];

        [Header("Networked Projectile Prefabs")]
        public GameObject throwingKnifePrefab;

        private Weapon activeWeapon;
        private int activeSlotIndex = 0;
        private PlayerNetworkState playerNetworkState;

        public Weapon ActiveWeapon => activeWeapon;
        public int ActiveSlotIndex => activeSlotIndex;

        // Legacy compatibility properties
        public Weapon slot1Weapon => (weaponSlots != null && weaponSlots.Length > 0) ? weaponSlots[0] : null;
        public Weapon slot2Weapon => (weaponSlots != null && weaponSlots.Length > 1) ? weaponSlots[1] : null;

        private void Awake()
        {
            playerNetworkState = GetComponent<PlayerNetworkState>();
            EnsureWeaponHolderAndWeapons();
        }

        private void Start()
        {
            InitializeLoadoutForCurrentRole();
        }

        public void EnsureWeaponHolderAndWeapons()
        {
            Transform holder = transform.Find("WeaponHolder");
            if (holder == null)
            {
                GameObject holderObj = new GameObject("WeaponHolder");
                holderObj.transform.SetParent(transform, false);
                holderObj.transform.localPosition = new Vector3(0.25f, 0.5f, 0.4f);
                holder = holderObj.transform;
            }

            if (weaponSlots == null || weaponSlots.Length != 3)
            {
                weaponSlots = new Weapon[3];
            }
        }

        public void InitializeLoadoutForCurrentRole()
        {
            PlayerRole currentRole = PlayerRole.Police;
            if (playerNetworkState != null && playerNetworkState.Role != PlayerRole.Unassigned)
            {
                currentRole = playerNetworkState.Role;
            }

            SetupRoleLoadout(currentRole);
        }

        public void SetupRoleLoadout(PlayerRole role)
        {
            Transform holder = transform.Find("WeaponHolder");
            if (holder == null)
            {
                EnsureWeaponHolderAndWeapons();
                holder = transform.Find("WeaponHolder");
            }

            if (role == PlayerRole.Assassin)
            {
                SetupAssassinWeapons(holder);
            }
            else
            {
                SetupPoliceWeapons(holder);
            }

            // Default equip Slot 0
            EquipSlot(0);
        }

        private void SetupAssassinWeapons(Transform holder)
        {
            weaponSlots = new Weapon[3];

            // 1. Slot 0 (Key 1): Slash Knife (Melee, Infinite)
            Transform knifeTrans = holder.Find("Weapon_SlashKnife");
            if (knifeTrans == null)
            {
                GameObject knifeObj = new GameObject("Weapon_SlashKnife");
                knifeObj.transform.SetParent(holder, false);
                knifeTrans = knifeObj.transform;
            }

            Weapon slashKnife = knifeTrans.GetComponent<Weapon>();
            if (slashKnife == null) slashKnife = knifeTrans.gameObject.AddComponent<Weapon>();

            WeaponData knifeData = ScriptableObject.CreateInstance<WeaponData>();
            knifeData.weaponName = "Slash Knife";
            knifeData.weaponType = WeaponType.Melee;
            knifeData.baseDamage = 100f; // Assassin Melee 100 HP
            knifeData.damage = 100f;
            knifeData.meleeRange = 1.8f;
            knifeData.maxRange = 1.8f;
            knifeData.meleeArcAngle = 90f;
            knifeData.fireRate = 0.5f;
            knifeData.viewAngle = 360f;
            knifeData.viewDistance = 12f;
            knifeData.requiresLineOfSightMultiplier = false;
            knifeData.isInfiniteAmmo = true;
            knifeData.canReload = false;
            slashKnife.weaponData = knifeData;
            slashKnife.InitializeAmmo();

            weaponSlots[0] = slashKnife;

            // 2. Slot 1 (Key 2): Throwing Knives (Throwable, 25 Knives, Non-reloadable)
            Transform throwTrans = holder.Find("Weapon_ThrowingKnives");
            if (throwTrans == null)
            {
                GameObject throwObj = new GameObject("Weapon_ThrowingKnives");
                throwObj.transform.SetParent(holder, false);
                throwTrans = throwObj.transform;
            }

            Weapon throwingKnives = throwTrans.GetComponent<Weapon>();
            if (throwingKnives == null) throwingKnives = throwTrans.gameObject.AddComponent<Weapon>();

            WeaponData throwData = ScriptableObject.CreateInstance<WeaponData>();
            throwData.weaponName = "Throwing Knives";
            throwData.weaponType = WeaponType.Throwable;
            throwData.baseDamage = 10f; // Throwing Knife 10 HP
            throwData.damage = 10f;
            throwData.maxRange = 15f;
            throwData.range = 15f;
            throwData.projectileSpeed = 22.0f;
            throwData.fireRate = 0.4f;
            throwData.maxAmmo = 25;
            throwData.viewAngle = 360f;
            throwData.viewDistance = 12f;
            throwData.requiresLineOfSightMultiplier = false;
            throwData.isInfiniteAmmo = false;
            throwData.canReload = false;
            throwingKnives.weaponData = throwData;
            throwingKnives.InitializeAmmo();

            weaponSlots[1] = throwingKnives;
            weaponSlots[2] = null;

            Debug.Log("[PlayerWeaponManager] Configured Assassin Loadout: Slot 0 (Slash Knife - 100 HP), Slot 1 (Throwing Knives - 10 HP x25).");
        }

        private void SetupPoliceWeapons(Transform holder)
        {
            weaponSlots = CreatePoliceWeaponSlots(holder);
        }

        private Weapon[] CreatePoliceWeaponSlots(Transform holder)
        {
            Weapon[] slots = new Weapon[3];

            // 1. Slot 0 (Key 1): Tactical Rifle (Narrow beam, Long range)
            Transform rifleTrans = holder.Find("Gun_Rifle");
            if (rifleTrans == null) rifleTrans = holder.Find("Weapon_Rifle");
            if (rifleTrans == null)
            {
                GameObject rifleObj = new GameObject("Gun_Rifle");
                rifleObj.transform.SetParent(holder, false);
                rifleTrans = rifleObj.transform;
            }

            Weapon rifle = rifleTrans.GetComponent<Weapon>();
            if (rifle == null) rifle = rifleTrans.gameObject.AddComponent<Gun>();

            WeaponData rifleData = ScriptableObject.CreateInstance<WeaponData>();
            rifleData.weaponName = "Tactical Rifle";
            rifleData.weaponType = WeaponType.Firearm;
            rifleData.baseDamage = 20f; // Police Gun 20 HP
            rifleData.damage = 20f;
            rifleData.maxRange = 30f;
            rifleData.range = 30f;
            rifleData.fireRate = 0.12f;
            rifleData.maxAmmo = 30;
            rifleData.reloadTime = 2.0f;
            rifleData.viewAngle = 45.0f;
            rifleData.viewDistance = 24.0f;
            rifleData.requiresLineOfSightMultiplier = true;
            rifleData.isInfiniteReserve = true;
            rifleData.canReload = true;
            rifle.weaponData = rifleData;
            rifle.InitializeAudio();
            rifle.InitializeAmmo();

            slots[0] = rifle;

            // 2. Slot 1 (Key 2): Combat Shotgun (Flood light, CQC Wide spread)
            Transform shotgunTrans = holder.Find("Gun_Shotgun");
            if (shotgunTrans == null) shotgunTrans = holder.Find("Weapon_Shotgun");
            if (shotgunTrans == null)
            {
                GameObject shotgunObj = new GameObject("Gun_Shotgun");
                shotgunObj.transform.SetParent(holder, false);
                shotgunTrans = shotgunObj.transform;
            }

            Weapon shotgun = shotgunTrans.GetComponent<Weapon>();
            if (shotgun == null) shotgun = shotgunTrans.gameObject.AddComponent<Gun>();

            WeaponData shotgunData = ScriptableObject.CreateInstance<WeaponData>();
            shotgunData.weaponName = "Combat Shotgun";
            shotgunData.weaponType = WeaponType.Firearm;
            shotgunData.baseDamage = 20f; // 20 HP
            shotgunData.damage = 20f;
            shotgunData.pellets = 1;
            shotgunData.maxRange = 30f;
            shotgunData.range = 30f;
            shotgunData.fireRate = 0.7f;
            shotgunData.maxAmmo = 8;
            shotgunData.reloadTime = 2.5f;
            shotgunData.viewAngle = 110.0f;
            shotgunData.viewDistance = 10.0f;
            shotgunData.requiresLineOfSightMultiplier = true;
            shotgunData.isInfiniteReserve = true;
            shotgunData.canReload = true;
            shotgun.weaponData = shotgunData;
            shotgun.InitializeAudio();
            shotgun.InitializeAmmo();

            slots[1] = shotgun;

            // 3. Slot 2 (Key 3): Pistol (Balanced tactical cone)
            Transform pistolTrans = holder.Find("Gun_Pistol");
            if (pistolTrans == null) pistolTrans = holder.Find("Weapon_Pistol");
            if (pistolTrans == null)
            {
                GameObject pistolObj = new GameObject("Gun_Pistol");
                pistolObj.transform.SetParent(holder, false);
                pistolTrans = pistolObj.transform;
            }

            Weapon pistol = pistolTrans.GetComponent<Weapon>();
            if (pistol == null) pistol = pistolTrans.gameObject.AddComponent<Gun>();

            WeaponData pistolData = ScriptableObject.CreateInstance<WeaponData>();
            pistolData.weaponName = "Pistol";
            pistolData.weaponType = WeaponType.Firearm;
            pistolData.baseDamage = 20f; // 20 HP
            pistolData.damage = 20f;
            pistolData.maxRange = 30f;
            pistolData.range = 30f;
            pistolData.fireRate = 0.22f;
            pistolData.maxAmmo = 15;
            pistolData.reloadTime = 1.4f;
            pistolData.viewAngle = 75.0f;
            pistolData.viewDistance = 16.0f;
            pistolData.requiresLineOfSightMultiplier = true;
            pistolData.isInfiniteReserve = true;
            pistolData.canReload = true;
            pistol.weaponData = pistolData;
            pistol.InitializeAudio();
            pistol.InitializeAmmo();

            slots[2] = pistol;

            weaponSlots = slots;
            ResetAllWeaponStates();

            Debug.Log("[PlayerWeaponManager] Configured Police Loadout: Slot 0 (Rifle - 20 HP / 30m), Slot 1 (Shotgun - 20 HP / 30m), Slot 2 (Pistol - 20 HP / 30m).");
            return slots;
        }

        public void HideAllWeaponVisuals()
        {
            if (weaponSlots == null) return;
            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (weaponSlots[i] != null)
                {
                    weaponSlots[i].gameObject.SetActive(false);
                }
            }
        }

        public void ShowActiveWeaponVisual()
        {
            if (activeWeapon != null)
            {
                activeWeapon.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Resets state on all equipped weapons: cancels any active reload coroutines, resets isReloading to false,
        /// restores currentMagazineAmmo to maximum capacity, and enforces infinite reserves.
        /// </summary>
        public void ResetAllWeaponStates()
        {
            if (weaponSlots == null) return;

            for (int i = 0; i < weaponSlots.Length; i++)
            {
                Weapon weapon = weaponSlots[i];
                if (weapon != null)
                {
                    weapon.ResetWeaponState();
                }
            }

            Debug.Log($"[PlayerWeaponManager] ResetAllWeaponStates executed: all equipped weapons reset to max magazine capacity with infinite reserves and reload coroutines cancelled.");
        }

        /// <summary>
        /// Instantaneous hitscan throwing knife attack with zero physics impulses / knockback.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ExecuteThrowingKnifeRaycastServerRpc(Vector3 origin, Vector3 aimDirection)
        {
            float maxKnifeRange = 15f;
            LayerMask hitLayers = LayerMask.GetMask("Obstacle", "Player");
            if (hitLayers == 0) hitLayers = ~LayerMask.GetMask("Ignore Raycast", "UI");

            if (Physics.Raycast(origin, aimDirection.normalized, out RaycastHit hit, maxKnifeRange, hitLayers, QueryTriggerInteraction.Ignore))
            {
                // If obstacle wall is hit first, terminate line trace (no damage behind cover)
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Obstacle") || hit.collider.CompareTag("Obstacle") || hit.collider.gameObject.layer == 7)
                {
                    Debug.Log("[THROWING KNIFE] Hit wall obstacle. Attack terminated.");
                    return;
                }

                // If player entity is hit, apply clean 10 HP damage without physics impulses
                var targetHealth = hit.collider.GetComponent<HealthComponent>() ?? hit.collider.GetComponentInParent<HealthComponent>();
                if (targetHealth != null && targetHealth.gameObject != gameObject)
                {
                    targetHealth.TakeDamageServerRpc(10f); // 10 HP Damage per knife hit
                    Debug.Log($"[THROWING KNIFE HIT] Applied 10 HP to target. Current HP: {targetHealth.CurrentHealth.Value}");
                }
            }
        }

        private void Update()
        {
            // If dead, block all weapon input
            var health = GetComponent<HealthComponent>();
            if (health != null && !health.IsAlive.Value) return;

            // If networked, only accept input from the owning client
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && netObj.IsSpawned && !netObj.IsOwner) return;

            // 3-Weapon Slot Swapping
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                EquipSlot(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                EquipSlot(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                EquipSlot(2);
            }

            if (activeWeapon == null) return;

            // Firing & Reloading Input
            if (Input.GetButton("Fire1") || Input.GetMouseButton(0))
            {
                activeWeapon.Shoot();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                activeWeapon.Reload();
            }
        }

        public void EquipSlot(int slotIndex)
        {
            if (weaponSlots == null || slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
            Weapon targetWeapon = weaponSlots[slotIndex];
            if (targetWeapon == null) return;

            activeSlotIndex = slotIndex;

            // 1. Cancel and stop reload on all outgoing weapons BEFORE deactivating them
            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (weaponSlots[i] != null && i != slotIndex)
                {
                    weaponSlots[i].CancelReload();
                    weaponSlots[i].gameObject.SetActive(false);
                }
            }

            // 2. Activate target weapon FIRST before any operations
            targetWeapon.gameObject.SetActive(true);
            targetWeapon.ResetReloadState();

            activeWeapon = targetWeapon;
            Debug.Log($"[PlayerWeaponManager] Equipped Slot {slotIndex}: {activeWeapon.weaponData?.weaponName ?? activeWeapon.name}");

            // 3. Notify dynamic vision cone for smooth real-time FOV transition (Police only)
            if (playerNetworkState == null || playerNetworkState.Role == PlayerRole.Police)
            {
                if (activeWeapon.weaponData != null)
                {
                    var dynamicFov = GetComponentInChildren<DynamicFOV>();
                    if (dynamicFov != null)
                    {
                        dynamicFov.UpdateWeaponVisionProfile(activeWeapon.weaponData.viewAngle, activeWeapon.weaponData.viewDistance, 0.2f);
                    }
                }
            }

            // 4. Sync visual model ID to network observers
            if (playerNetworkState != null && playerNetworkState.IsOwner)
            {
                playerNetworkState.CmdSelectWeapon(slotIndex);
            }
        }

        // Backward compatibility helpers
        public void EquipSlot1() => EquipSlot(0);
        public void EquipSlot2() => EquipSlot(1);
    }
}
