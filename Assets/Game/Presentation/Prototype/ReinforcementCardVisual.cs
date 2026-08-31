using System;
using System.Collections.Generic;
using FortressFrontier.Runtime.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class ReinforcementCardVisual : MonoBehaviour
    {
        [SerializeField] private Image[] _unitIcons = Array.Empty<Image>();
        [SerializeField] private Text[] _quantityTexts = Array.Empty<Text>();
        [SerializeField] private Text _reinforcementLabel;
        [SerializeField] private Text _titleText;

        public IReadOnlyList<Image> UnitIcons => _unitIcons;
        public IReadOnlyList<Text> QuantityTexts => _quantityTexts;
        public Text ReinforcementLabel => _reinforcementLabel;
        public Text TitleText => _titleText;

        public void Bind(IGameplaySpriteResolver sprites, IReadOnlyList<ReinforcementUnitViewModel> units, string title = "")
        {
            var count = units?.Count ?? 0;
            gameObject.SetActive(count > 0);
            if (count == 0) return;
            if (sprites == null) throw new ArgumentNullException(nameof(sprites));
            if (_unitIcons.Length != 3 || _quantityTexts.Length != 3)
                throw new InvalidOperationException("ReinforcementCardVisual requires exactly three fixed icon slots.");
            if (_titleText != null) _titleText.text = title ?? string.Empty;

            for (var index = 0; index < 3; index++) SetSlot(index, false, null, 0);
            if (count == 1) SetSlot(1, true, units[0], units[0].Quantity);
            else if (count == 2)
            {
                SetSlot(0, true, units[0], units[0].Quantity);
                SetSlot(2, true, units[1], units[1].Quantity);
            }
            else
            {
                for (var index = 0; index < 3; index++) SetSlot(index, true, units[index], units[index].Quantity);
            }

            void SetSlot(int index, bool visible, ReinforcementUnitViewModel unit, int quantity)
            {
                _unitIcons[index].gameObject.SetActive(visible);
                if (!visible) return;
                _unitIcons[index].sprite = sprites.Resolve(unit.SpriteKey);
                _quantityTexts[index].text = $"×{quantity}";
            }
        }
    }
}
