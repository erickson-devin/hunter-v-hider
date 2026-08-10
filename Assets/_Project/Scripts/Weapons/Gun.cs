using UnityEngine;
using System.Collections;

namespace HunterVsHider.Weapons
{
    public class Gun : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform muzzlePoint;

        [Header("Gun Stats")]
        [SerializeField] private int maxAmmo = 30;
        [SerializeField] private float fireRate = 0.15f;
        [SerializeField] private float reloadTime = 2.0f;

        [Header("Audio")]
        [SerializeField] private AudioClip fireSFX;
        [SerializeField] private AudioClip reloadSFX;
        [SerializeField] private AudioClip dryFireSFX;
        [SerializeField] private AudioClip impactSFX;

        private AudioSource audioSource;

        private int currentAmmo;
        private float nextFireTime = 0f;
        private bool isReloading = false;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.spatialBlend = 0.2f;

            if (fireSFX == null) fireSFX = CreateSynthClip("Fire", 440f, 0.1f);
            if (reloadSFX == null) reloadSFX = CreateSynthClip("Reload", 220f, 0.5f);
            if (dryFireSFX == null) dryFireSFX = CreateSynthClip("DryFire", 880f, 0.05f);
            if (impactSFX == null) impactSFX = CreateSynthClip("Impact", 100f, 0.15f);
        }

        private void Start()
        {
            currentAmmo = maxAmmo;
            
            if (muzzlePoint == null)
            {
                muzzlePoint = transform.Find("MuzzlePoint");
            }
        }

        private void OnDisable()
        {
            // Stop running reload coroutines and reset reloading state lock
            StopAllCoroutines();
            isReloading = false;
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

        public void TryFire()
        {
            Shoot();
        }

        public void Shoot()
        {
            if (isReloading || Time.time < nextFireTime)
            {
                return;
            }

            if (currentAmmo <= 0)
            {
                if (audioSource != null && dryFireSFX != null)
                {
                    audioSource.PlayOneShot(dryFireSFX);
                }
                nextFireTime = Time.time + fireRate;
                return;
            }

            if (muzzlePoint == null)
            {
                Debug.LogWarning("MuzzlePoint is missing on Gun!");
                return;
            }

            // Update state
            nextFireTime = Time.time + fireRate;
            currentAmmo--;

            // Gunshot sound at muzzle
            if (audioSource != null && fireSFX != null) audioSource.PlayOneShot(fireSFX, 1.0f);

            // Perform 3D Raycast
            Ray ray = new Ray(muzzlePoint.position, muzzlePoint.forward);
            int layerMask = ~(1 << 6); // Ignore Player layer
            float range = 50f;

            if (Physics.Raycast(ray, out RaycastHit hit, range, layerMask))
            {
                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(25f);
                }

                if (audioSource != null)
                {
                    if (impactSFX != null && impactSFX != fireSFX)
                    {
                        audioSource.PlayOneShot(impactSFX, 0.7f);
                    }
                    else if (fireSFX != null)
                    {
                        audioSource.pitch = 1.6f;
                        audioSource.PlayOneShot(fireSFX, 0.5f);
                        audioSource.pitch = 1.0f;
                    }
                }

                // Visualize the ray (hit)
                Debug.DrawRay(muzzlePoint.position, hit.point - muzzlePoint.position, Color.red, 2.0f);
            }
            else
            {
                // Visualize the ray (miss)
                Debug.DrawRay(muzzlePoint.position, muzzlePoint.forward * range, Color.red, 2.0f);
            }
            
            // Console output
            Debug.Log($"[{gameObject.name}] Fired | Ammo: {currentAmmo}/{maxAmmo}");
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
            Debug.Log("Reloading...");

            if (audioSource != null && reloadSFX != null)
            {
                audioSource.PlayOneShot(reloadSFX);
            }

            yield return new WaitForSeconds(reloadTime);

            currentAmmo = maxAmmo;
            isReloading = false;
            Debug.Log($"Reload Complete! Ammo: {currentAmmo}/{maxAmmo}");
        }
    }
}
