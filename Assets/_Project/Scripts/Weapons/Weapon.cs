using System.Collections;
using UnityEngine;

namespace HunterVsHider.Weapons
{
    public class Weapon : MonoBehaviour
    {
        [Header("Configuration")]
        public WeaponData weaponData;

        [Header("References")]
        [SerializeField] protected Transform muzzlePoint;

        [Header("Audio Settings")]
        [SerializeField] protected AudioClip fireSFX;
        [SerializeField] protected AudioClip reloadSFX;
        [SerializeField] protected AudioClip dryFireSFX;
        [SerializeField] protected AudioClip impactSFX;

        protected AudioSource audioSource;
        protected int currentAmmo;
        protected float nextTimeToFire = 0f;
        protected bool isReloading = false;

        public int CurrentAmmo => currentAmmo;
        public int currentMagazineAmmo { get => currentAmmo; set => currentAmmo = value; }
        public int MaxAmmo => (weaponData != null) ? weaponData.maxAmmo : 30;
        public int maxMagazineAmmo => MaxAmmo;
        public bool IsInfiniteAmmo => (weaponData != null) && weaponData.isInfiniteAmmo;
        public bool CanReload => (weaponData != null) && weaponData.canReload && !weaponData.isInfiniteAmmo;
        public bool IsReloading => isReloading;
        public WeaponType Type => (weaponData != null) ? weaponData.weaponType : WeaponType.Firearm;
        public Transform MuzzlePoint => muzzlePoint != null ? muzzlePoint : transform;

        public AudioClip FireSFX
        {
            get
            {
                if (weaponData != null)
                {
                    if (weaponData.fireSound != null) return weaponData.fireSound;
                    if (weaponData.attackSFX != null) return weaponData.attackSFX;
                }
                return fireSFX;
            }
        }

        protected virtual void Awake()
        {
            InitializeAudio();
            InitializeAmmo();
        }

        protected virtual void Start()
        {
            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("MuzzlePoint");
                if (muzzlePoint == null) muzzlePoint = transform;
            }
        }

        public void InitializeAmmo()
        {
            currentAmmo = (weaponData != null) ? weaponData.maxAmmo : 30;
            isReloading = false;
        }

        public virtual void ResetWeaponState()
        {
            StopAllCoroutines();
            isReloading = false;
            if (weaponData != null)
            {
                weaponData.isInfiniteReserve = true;
                currentAmmo = weaponData.maxAmmo;
            }
            else
            {
                currentAmmo = MaxAmmo;
            }
            InitializeAudio();
            Debug.Log($"[Weapon] ResetWeaponState on {gameObject.name}: currentMagazineAmmo = {currentAmmo}/{MaxAmmo}, isReloading = false, infiniteReserve = true.");
        }

        public virtual void InitializeAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.0f; // Ensure audible 2D/pan audio for player
            audioSource.volume = 1.0f;

            if (weaponData != null)
            {
                if (weaponData.fireSound != null) fireSFX = weaponData.fireSound;
                else if (weaponData.attackSFX != null) fireSFX = weaponData.attackSFX;

                if (weaponData.reloadSFX != null) reloadSFX = weaponData.reloadSFX;
                if (weaponData.dryFireSFX != null) dryFireSFX = weaponData.dryFireSFX;
                if (weaponData.impactSFX != null) impactSFX = weaponData.impactSFX;
            }

            if (fireSFX == null) fireSFX = CreateGunshotClip("Gunshot_Procedural", 160f, 0.18f);
            if (reloadSFX == null) reloadSFX = CreateSynthClip("Reload", 220f, 0.4f);
            if (dryFireSFX == null) dryFireSFX = CreateSynthClip("DryFire", 880f, 0.05f);
            if (impactSFX == null) impactSFX = CreateSynthClip("Impact", 100f, 0.15f);
        }

        protected AudioClip CreateGunshotClip(string clipName, float frequency, float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Exp(-t * 14f);
                float noise = (Random.value * 2f - 1f) * 0.65f;
                float punch = Mathf.Sin(2 * Mathf.PI * frequency * (1f - t * 0.6f) * i / sampleRate) * 0.35f;
                data[i] = (noise + punch) * env;
            }
            clip.SetData(data, 0);
            return clip;
        }

        protected AudioClip CreateSynthClip(string clipName, float frequency, float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                data[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate);
                float envelope = 1f - ((float)i / samples);
                data[i] *= envelope * 0.5f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        protected virtual void OnDisable()
        {
            StopAllCoroutines();
            isReloading = false;
        }

        public virtual void TryFire()
        {
            Shoot();
        }

        public virtual void Shoot()
        {
            if (isReloading) return;

            var health = GetComponentInParent<HunterVsHider.Player.HealthComponent>();
            if (health != null && !health.IsAlive.Value) return;

            WeaponType wType = (weaponData != null) ? weaponData.weaponType : WeaponType.Firearm;

            switch (wType)
            {
                case WeaponType.Melee:
                    ExecuteMeleeSlash();
                    break;
                case WeaponType.Throwable:
                    ExecuteThrowableLaunch();
                    break;
                case WeaponType.Firearm:
                default:
                    ExecuteFirearmShot();
                    break;
            }
        }

        protected virtual void ExecuteMeleeSlash()
        {
            float rate = (weaponData != null) ? weaponData.fireRate : 0.5f;
            if (Time.time < nextTimeToFire) return;
            nextTimeToFire = Time.time + rate;

            // Melee Audio Swing
            if (fireSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX, 0.7f);
            }

            float range = (weaponData != null) ? weaponData.meleeRange : 1.8f;
            float arcAngle = (weaponData != null) ? weaponData.meleeArcAngle : 90.0f;
            float dmg = (weaponData != null) ? weaponData.baseDamage : 100.0f; // Assassin 100 HP Melee

            Vector3 origin = (muzzlePoint != null) ? muzzlePoint.position : transform.position;
            Vector3 forward = transform.root.forward;

            int layerMask = ~LayerMask.GetMask("Ignore Raycast", "UI");
            Collider[] hits = Physics.OverlapSphere(origin, range, layerMask, QueryTriggerInteraction.Ignore);

            bool hitAny = false;
            foreach (var col in hits)
            {
                if (col.gameObject == gameObject || col.transform.IsChildOf(transform.root)) continue;

                Vector3 toTarget = col.bounds.center - origin;
                toTarget.y = 0f;

                if (toTarget.magnitude <= 0.05f || Vector3.Angle(forward, toTarget.normalized) <= arcAngle * 0.5f)
                {
                    if (col.TryGetComponent<HunterVsHider.Player.HealthComponent>(out var targetHealth))
                    {
                        targetHealth.TakeDamageServerRpc(dmg);
                        hitAny = true;
                        Debug.Log($"[Weapon] Melee Slash hit {col.gameObject.name} for {dmg} damage.");
                    }
                    else if (col.GetComponentInParent<HunterVsHider.Player.HealthComponent>() is var parentHealth && parentHealth != null)
                    {
                        parentHealth.TakeDamageServerRpc(dmg);
                        hitAny = true;
                        Debug.Log($"[Weapon] Melee Slash hit parent {col.gameObject.name} for {dmg} damage.");
                    }
                }
            }

            if (hitAny && impactSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(impactSFX, 0.8f);
            }
        }

        protected virtual void ExecuteThrowableLaunch()
        {
            if (currentAmmo <= 0 && MaxAmmo > 0)
            {
                if (dryFireSFX != null && audioSource != null)
                {
                    audioSource.PlayOneShot(dryFireSFX, 0.5f);
                }
                return;
            }

            float rate = (weaponData != null) ? weaponData.fireRate : 0.5f;
            if (Time.time < nextTimeToFire) return;
            nextTimeToFire = Time.time + rate;

            if (fireSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX, 0.8f);
            }

            if (!IsInfiniteAmmo && MaxAmmo > 0)
            {
                currentAmmo--;
            }

            GameObject prefab = (weaponData != null) ? weaponData.projectilePrefab : null;
            if (prefab == null)
            {
                Debug.LogWarning($"[Weapon] No projectilePrefab assigned on Throwable WeaponData: {name}");
                return;
            }

            Vector3 origin = (muzzlePoint != null) ? muzzlePoint.position : transform.position;
            Vector3 forward = transform.root.forward;
            float speed = (weaponData != null) ? weaponData.projectileSpeed : 22.0f;
            float dmg = (weaponData != null) ? weaponData.baseDamage : 10.0f; // Throwing Knife 10 HP

            GameObject proj = Instantiate(prefab, origin, Quaternion.LookRotation(forward));
            ThrowingKnife knife = proj.GetComponent<ThrowingKnife>();
            if (knife != null)
            {
                knife.Launch(forward, speed, dmg);
            }
        }

        protected virtual void ExecuteFirearmShot()
        {
            if (currentAmmo <= 0 && MaxAmmo > 0)
            {
                if (dryFireSFX != null && audioSource != null)
                {
                    audioSource.PlayOneShot(dryFireSFX, 0.5f);
                }
                return;
            }

            float rate = (weaponData != null) ? weaponData.fireRate : 0.15f;
            if (Time.time < nextTimeToFire) return;
            nextTimeToFire = Time.time + rate;

            // Audio
            if (fireSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX, 0.8f);
            }

            if (!IsInfiniteAmmo && MaxAmmo > 0)
            {
                currentAmmo--;
            }

            float range = (weaponData != null) ? weaponData.maxRange : 30.0f;
            float baseDmg = (weaponData != null) ? weaponData.baseDamage : 20.0f;
            bool requiresLOS = (weaponData != null) ? weaponData.requiresLineOfSightMultiplier : true;
            int pelletCount = (weaponData != null) ? Mathf.Max(1, weaponData.pellets) : 1;
            float spreadAngle = (weaponData != null) ? weaponData.spreadAngle : 0.0f;

            Vector3 origin = (muzzlePoint != null) ? muzzlePoint.position : transform.position;
            Vector3 forward = (muzzlePoint != null) ? muzzlePoint.forward : transform.forward;

            // Capture 2D aim vector toward mouse cursor world position
            Vector3 aimDirection = forward;
            aimDirection.y = 0f;

            if (Camera.main != null)
            {
                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, 0.5f, 0f));
                if (groundPlane.Raycast(mouseRay, out float enter))
                {
                    Vector3 mouseWorldPosition = mouseRay.GetPoint(enter);
                    Vector3 calculatedAim = (mouseWorldPosition - origin).normalized;
                    calculatedAim.y = 0f;
                    if (calculatedAim.sqrMagnitude > 0.001f)
                    {
                        aimDirection = calculatedAim.normalized;
                    }
                }
            }

            int layerMask = ~LayerMask.GetMask("Ignore Raycast", "UI");

            for (int p = 0; p < pelletCount; p++)
            {
                Vector3 shotDir = forward;
                if (spreadAngle > 0.01f && pelletCount > 1)
                {
                    float randX = Random.Range(-spreadAngle, spreadAngle) * 0.5f;
                    float randY = Random.Range(-spreadAngle, spreadAngle) * 0.5f;
                    shotDir = Quaternion.Euler(randX, randY, 0f) * forward;
                }

                if (Physics.Raycast(origin, shotDir, out RaycastHit hit, range, layerMask, QueryTriggerInteraction.Ignore))
                {
                    if (p == 0 && impactSFX != null)
                    {
                        AudioSource.PlayClipAtPoint(impactSFX, hit.point, 0.6f);
                    }

                    // If raycast hits Layer 7 (Obstacle), play impact and terminate
                    if (hit.collider.gameObject.layer == 7 || hit.collider.CompareTag("Obstacle"))
                    {
                        Debug.DrawRay(origin, hit.point - origin, Color.gray, 2.0f);
                        continue;
                    }

                    // Check for HealthComponent on target or parent
                    HunterVsHider.Player.HealthComponent targetHealth = hit.collider.GetComponent<HunterVsHider.Player.HealthComponent>();
                    if (targetHealth == null)
                    {
                        targetHealth = hit.collider.GetComponentInParent<HunterVsHider.Player.HealthComponent>();
                    }

                    if (targetHealth != null && targetHealth.gameObject != transform.root.gameObject)
                    {
                        targetHealth.TakeDamageServerRpc(baseDmg);
                    }

                    Debug.DrawRay(origin, hit.point - origin, Color.red, 2.0f);
                }
                else
                {
                    Debug.DrawRay(origin, shotDir * range, Color.red, 2.0f);
                }
            }
        }

        public virtual void Reload()
        {
            if (!gameObject.activeInHierarchy) return;
            if (!CanReload || isReloading || currentAmmo >= MaxAmmo)
            {
                return;
            }

            StartCoroutine(ReloadCoroutine());
        }

        public void CancelReload()
        {
            StopAllCoroutines();
            isReloading = false;
        }

        public void ResetReloadState()
        {
            StopAllCoroutines();
            isReloading = false;
        }

        protected virtual IEnumerator ReloadCoroutine()
        {
            isReloading = true;

            if (reloadSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(reloadSFX, 0.7f);
            }

            float time = (weaponData != null) ? weaponData.reloadTime : 1.5f;
            yield return new WaitForSeconds(time);

            // With infinite reserves (isInfiniteReserve = true), magazines always fill to 100% capacity
            currentAmmo = MaxAmmo;
            isReloading = false;
        }
    }
}
