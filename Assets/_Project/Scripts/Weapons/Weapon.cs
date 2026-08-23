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
        public int MaxAmmo => (weaponData != null) ? weaponData.maxAmmo : 30;
        public bool IsInfiniteAmmo => (weaponData != null) && weaponData.isInfiniteAmmo;
        public bool CanReload => (weaponData != null) && weaponData.canReload && !weaponData.isInfiniteAmmo;
        public bool IsReloading => isReloading;
        public WeaponType Type => (weaponData != null) ? weaponData.weaponType : WeaponType.Firearm;

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

        protected virtual void InitializeAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.2f;
            }

            if (fireSFX == null && weaponData != null && weaponData.attackSFX != null) fireSFX = weaponData.attackSFX;
            if (reloadSFX == null && weaponData != null && weaponData.reloadSFX != null) reloadSFX = weaponData.reloadSFX;
            if (dryFireSFX == null && weaponData != null && weaponData.dryFireSFX != null) dryFireSFX = weaponData.dryFireSFX;
            if (impactSFX == null && weaponData != null && weaponData.impactSFX != null) impactSFX = weaponData.impactSFX;

            if (fireSFX == null) fireSFX = CreateSynthClip("Attack", 440f, 0.1f);
            if (reloadSFX == null) reloadSFX = CreateSynthClip("Reload", 220f, 0.5f);
            if (dryFireSFX == null) dryFireSFX = CreateSynthClip("DryFire", 880f, 0.05f);
            if (impactSFX == null) impactSFX = CreateSynthClip("Impact", 100f, 0.15f);
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
            float dmg = (weaponData != null) ? weaponData.damage : 50.0f;

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
                    IDamageable damageable = col.GetComponent<IDamageable>();
                    if (damageable == null) damageable = col.GetComponentInParent<IDamageable>();

                    if (damageable != null)
                    {
                        damageable.TakeDamage(dmg);
                        hitAny = true;
                        Debug.Log($"[Weapon] Melee Slash hit {col.gameObject.name} for {dmg} damage.");
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
            if (currentAmmo <= 0)
            {
                if (dryFireSFX != null && audioSource != null)
                {
                    audioSource.PlayOneShot(dryFireSFX, 0.5f);
                }
                return;
            }

            float rate = (weaponData != null) ? weaponData.fireRate : 0.4f;
            if (Time.time < nextTimeToFire) return;
            nextTimeToFire = Time.time + rate;

            currentAmmo--;

            if (fireSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX, 0.7f);
            }

            Vector3 spawnPos = (muzzlePoint != null) ? muzzlePoint.position : (transform.position + transform.root.forward * 0.5f);
            Vector3 spawnDir = transform.root.forward;

            GameObject prefab = (weaponData != null && weaponData.projectilePrefab != null) ? weaponData.projectilePrefab : null;

            if (prefab != null)
            {
                GameObject projObj = Instantiate(prefab, spawnPos, Quaternion.LookRotation(spawnDir));
                var knife = projObj.GetComponent<ThrowingKnife>();
                if (knife != null)
                {
                    float speed = (weaponData != null) ? weaponData.projectileSpeed : 22.0f;
                    float dmg = (weaponData != null) ? weaponData.damage : 75.0f;
                    knife.Launch(spawnDir, speed, dmg);
                }
            }
            else
            {
                // Fallback procedural projectile
                GameObject primitiveKnife = GameObject.CreatePrimitive(PrimitiveType.Cube);
                primitiveKnife.name = "Procedural_ThrowingKnife";
                primitiveKnife.transform.position = spawnPos;
                primitiveKnife.transform.localScale = new Vector3(0.08f, 0.08f, 0.4f);
                primitiveKnife.transform.forward = spawnDir;

                var tk = primitiveKnife.AddComponent<ThrowingKnife>();
                float speed = (weaponData != null) ? weaponData.projectileSpeed : 22.0f;
                float dmg = (weaponData != null) ? weaponData.damage : 75.0f;
                tk.Launch(spawnDir, speed, dmg);
            }
        }

        protected virtual void ExecuteFirearmShot()
        {
            if (currentAmmo <= 0)
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

            currentAmmo--;

            if (fireSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX, 0.8f);
            }

            float range = (weaponData != null) ? weaponData.range : 50.0f;
            float dmg = (weaponData != null) ? weaponData.damage : 25.0f;
            int pelletCount = (weaponData != null) ? Mathf.Max(1, weaponData.pellets) : 1;
            float spreadAngle = (weaponData != null) ? weaponData.spreadAngle : 0.0f;

            Vector3 origin = (muzzlePoint != null) ? muzzlePoint.position : transform.position;
            Vector3 forward = (muzzlePoint != null) ? muzzlePoint.forward : transform.forward;

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

                    IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                    if (damageable == null) damageable = hit.collider.GetComponentInParent<IDamageable>();

                    if (damageable != null)
                    {
                        damageable.TakeDamage(dmg);
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
