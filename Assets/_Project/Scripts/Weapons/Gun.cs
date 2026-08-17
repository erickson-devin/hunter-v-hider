using UnityEngine;
using System.Collections;

namespace HunterVsHider.Weapons
{
    public class Gun : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform muzzlePoint;

        public Transform MuzzlePoint => muzzlePoint;

        [Header("Gun Stats")]
        [SerializeField] private int maxAmmo = 30;
        [SerializeField] private float fireRate = 0.15f;
        [SerializeField] private float reloadTime = 2.0f;
        [SerializeField] private float maxRange = 50f;
        [SerializeField] private float damage = 25f;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip fireSFX;
        [SerializeField] private AudioClip reloadSFX;
        [SerializeField] private AudioClip dryFireSFX;
        [SerializeField] private AudioClip impactSFX;

        private AudioSource audioSource;
        private int currentAmmo;
        private float nextTimeToFire = 0f;
        private bool isReloading = false;

        private void Awake()
        {
            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("MuzzlePoint");
            }
            currentAmmo = maxAmmo;
            isReloading = false;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.2f; // 2D/3D hybrid to prevent camera distance attenuation
            }

            if (fireSFX == null) fireSFX = CreateSynthClip("Fire", 440f, 0.1f);
            if (reloadSFX == null) reloadSFX = CreateSynthClip("Reload", 220f, 0.5f);
            if (dryFireSFX == null) dryFireSFX = CreateSynthClip("DryFire", 880f, 0.05f);
            impactSFX = impactSFX ?? CreateSynthClip("Impact", 100f, 0.15f);
        }

        private void Start()
        {
            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("MuzzlePoint");
            }
        }

        private AudioClip CreateSynthClip(string name, float frequency, float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
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

        private void OnDisable()
        {
            StopAllCoroutines();
            isReloading = false;
        }

        public void TryFire()
        {
            Shoot();
        }

        public void Shoot()
        {
            if (isReloading) return;

            // Dry Fire Audio Feedback
            if (currentAmmo <= 0)
            {
                if (dryFireSFX != null && audioSource != null)
                {
                    audioSource.PlayOneShot(dryFireSFX, 0.5f);
                }
                return;
            }

            if (Time.time < nextTimeToFire) return;

            nextTimeToFire = Time.time + fireRate;
            currentAmmo--;

            // Gunshot Audio
            if (fireSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX, 0.8f);
            }

            int layerMask = ~LayerMask.GetMask("Ignore Raycast", "UI");

            if (Physics.Raycast(muzzlePoint.position, muzzlePoint.forward, out RaycastHit hit, maxRange, layerMask, QueryTriggerInteraction.Ignore))
            {
                // Impact SFX at hit location
                if (impactSFX != null)
                {
                    AudioSource.PlayClipAtPoint(impactSFX, hit.point, 0.6f);
                }

                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                }

                Debug.DrawRay(muzzlePoint.position, hit.point - muzzlePoint.position, Color.red, 2.0f);
            }
            else
            {
                Debug.DrawRay(muzzlePoint.position, muzzlePoint.forward * maxRange, Color.red, 2.0f);
            }
        }

        public void Reload()
        {
            if (isReloading || currentAmmo >= maxAmmo)
            {
                return;
            }

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            
            // Reload Audio
            if (reloadSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(reloadSFX, 0.7f);
            }

            yield return new WaitForSeconds(reloadTime);
            currentAmmo = maxAmmo;
            isReloading = false;
        }
    }
}
