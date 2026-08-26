using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using HunterVsHider.Player;
using HunterVsHider.Managers;

namespace HunterVsHider.UI
{
    /// <summary>
    /// Screen-Space HUD controller for the local player.
    /// Displays a real-time health bar slider/fill and numeric text (e.g. "100 / 100 HP").
    /// Subscribes to HealthComponent.CurrentHealth.OnValueChanged to update automatically.
    /// Strictly rendered only for IsLocalPlayer / IsOwner and ONLY during MatchState.CombatPhase.
    /// </summary>
    public class PlayerHUD : NetworkBehaviour
    {
        public static PlayerHUD LocalInstance { get; private set; }

        [Header("UI Component Bindings (Optional Inspector Assignment)")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Canvas hudCanvas;

        [Header("Visual Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.9f, 0.3f, 1f); // Green
        [SerializeField] private Color midHealthColor = new Color(1.0f, 0.8f, 0.1f, 1f);  // Yellow
        [SerializeField] private Color lowHealthColor = new Color(0.95f, 0.2f, 0.2f, 1f); // Red
        [SerializeField] private Color hudBgColor = new Color(0.04f, 0.07f, 0.12f, 0.90f);

        private HealthComponent healthComponent;
        private PlayerNetworkState playerState;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = GetComponentInParent<HealthComponent>();
            }

            playerState = GetComponent<PlayerNetworkState>();
            if (playerState == null)
            {
                playerState = GetComponentInParent<PlayerNetworkState>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner || IsLocalPlayer)
            {
                LocalInstance = this;

                if (healthComponent != null)
                {
                    healthComponent.CurrentHealth.OnValueChanged += HandleHealthValueChanged;
                    healthComponent.MaxHealth.OnValueChanged += HandleMaxHealthValueChanged;

                    UpdateHUDDisplay(healthComponent.HealthValue, healthComponent.MaxHealthValue);
                }

                if (MatchManager.Instance != null)
                {
                    MatchManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;
                    UpdateVisibilityForState(MatchManager.Instance.CurrentState);
                }
                else
                {
                    UpdateVisibilityForState(MatchState.WaitingForPlayers);
                }
            }
            else
            {
                // Disable canvas on remote observer instances
                if (hudCanvas != null)
                {
                    hudCanvas.enabled = false;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (healthComponent != null)
            {
                healthComponent.CurrentHealth.OnValueChanged -= HandleHealthValueChanged;
                healthComponent.MaxHealth.OnValueChanged -= HandleMaxHealthValueChanged;
            }

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
            }

            if (LocalInstance == this)
            {
                LocalInstance = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner && !IsLocalPlayer) return;

            // Runtime fallback sync for match state visibility
            if (MatchManager.Instance != null)
            {
                UpdateVisibilityForState(MatchManager.Instance.CurrentState);
            }
        }

        private void HandleMatchStateChanged(MatchState previousState, MatchState newState)
        {
            UpdateVisibilityForState(newState);
        }

        private void UpdateVisibilityForState(MatchState state)
        {
            bool isCombat = (state == MatchState.CombatPhase);
            bool shouldShow = (IsOwner || IsLocalPlayer) && isCombat;

            if (hudCanvas != null)
            {
                hudCanvas.enabled = shouldShow;
            }
        }

        private void HandleHealthValueChanged(float previousValue, float newValue)
        {
            if (healthComponent != null)
            {
                UpdateHUDDisplay(newValue, healthComponent.MaxHealthValue);
            }
        }

        private void HandleMaxHealthValueChanged(float previousValue, float newValue)
        {
            if (healthComponent != null)
            {
                UpdateHUDDisplay(healthComponent.HealthValue, newValue);
            }
        }

        /// <summary>
        /// Updates the slider value, fill color, and text representation.
        /// </summary>
        public void UpdateHUDDisplay(float currentHp, float maxHp)
        {
            float pct = Mathf.Clamp01(currentHp / Mathf.Max(1f, maxHp));

            if (healthSlider != null)
            {
                healthSlider.value = pct;
            }

            Color currentColor = pct > 0.5f 
                ? Color.Lerp(midHealthColor, fullHealthColor, (pct - 0.5f) * 2f)
                : Color.Lerp(lowHealthColor, midHealthColor, pct * 2f);

            if (healthFillImage != null)
            {
                healthFillImage.color = currentColor;
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)} HP";
            }
        }

        private void OnGUI()
        {
            // Only render local screen overlay for the owner / local player
            if (!IsOwner && !IsLocalPlayer) return;
            if (healthComponent == null || !healthComponent.IsSpawned) return;

            // Restrict visibility strictly to CombatPhase
            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.CombatPhase)
            {
                return;
            }

            // If a custom UI Canvas with Slider & Text is active and rendered via UGUI, skip OnGUI fallback
            if (healthSlider != null && healthText != null && hudCanvas != null && hudCanvas.enabled) return;

            // Fallback runtime Screen-Space HUD drawn at bottom-left
            float curHp = healthComponent.HealthValue;
            float maxHp = healthComponent.MaxHealthValue;
            float pct = healthComponent.GetHealthNormalized();

            int hudW = 260;
            int hudH = 75;
            int x = 25;
            int y = Screen.height - hudH - 25;

            // Container Box
            GUI.backgroundColor = hudBgColor;
            GUILayout.BeginArea(new Rect(x, y, hudW, hudH), GUI.skin.box);

            string roleTag = (playerState != null && playerState.Role == PlayerRole.Assassin) 
                ? "<color=#FF3366><b>ASSASSIN</b></color>" 
                : "<color=#33CCFF><b>POLICE OFFICER</b></color>";

            GUILayout.Space(2);
            GUILayout.Label($"{roleTag} | <b>{Mathf.CeilToInt(curHp)} / {Mathf.CeilToInt(maxHp)} HP</b>");

            // Health bar container
            Rect barRect = GUILayoutUtility.GetRect(hudW - 20, 22);
            GUI.color = new Color(0.12f, 0.15f, 0.20f, 1.0f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            // Dynamic Fill Color
            Color barColor = pct > 0.5f 
                ? Color.Lerp(midHealthColor, fullHealthColor, (pct - 0.5f) * 2f)
                : Color.Lerp(lowHealthColor, midHealthColor, pct * 2f);

            if (pct < 0.20f) barColor = lowHealthColor;

            GUI.color = barColor;
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
