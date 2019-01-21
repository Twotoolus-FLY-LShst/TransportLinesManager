﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using Klyte.Commons.Extensors;
using Klyte.Commons.Utils;
using Klyte.TransportLinesManager.Extensors.TransportTypeExt;
using Klyte.TransportLinesManager.Interfaces;
using Klyte.TransportLinesManager.OptionsMenu;
using Klyte.TransportLinesManager.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace Klyte.TransportLinesManager.UI
{

    internal abstract class TLMTabControllerPrefixList<T> : UICustomControl, IBudgetControlParentInterface where T : TLMSysDef<T>
    {
        public UIScrollablePanel mainPanel { get; private set; }
        public UIHelperExtension m_uiHelper;
        private static TLMTabControllerPrefixList<T> m_instance;
        public static TLMTabControllerPrefixList<T> instance => m_instance;

        private UIDropDown m_prefixSelector;
        private UIHelperExtension m_subpanel;
        private UIPanel m_panelAssetSelector;

        private UIColorField m_prefixColor;
        private UITextField m_prefixName;
        private UICheckBox m_useColorForModel;
        private UITextField m_prefixTicketPrice;

        UILabel m_lineBudgetSlidersTitle;
        UISlider[] m_budgetSliders = new UISlider[8];
        private UIButton m_enableBudgetPerHour;
        private UIButton m_disableBudgetPerHour;

        private UIDropDown m_paletteDD;
        private UIDropDown m_formatDD;

        private TLMAssetSelectorWindowPrefixTab<T> m_assetSelectorWindow;

        public static OnPrefixLoad eventOnPrefixChange;
        public static OnColorChanged eventOnColorChange;
        public int SelectedPrefix => m_prefixSelector?.selectedIndex ?? -1;


        private static ITLMTransportTypeExtension extension => Singleton<T>.instance.GetTSD().GetTransportExtension();

        private static Type ImplClassChildren => TLMUtils.GetImplementationForGenericType(typeof(TLMAssetSelectorWindowPrefixTab<>), typeof(T));

        public ushort CurrentSelectedId => throw new NotImplementedException();

        public bool PrefixSelectionMode => true;


        public TransportSystemDefinition TransportSystem => Singleton<T>.instance.GetTSD();


        #region Awake
        private void Awake()
        {
            if (m_instance != null) throw new Exception("MULTIPLE INSTANTIATION!!!!!!!!");
            m_instance = this;
            mainPanel = GetComponentInChildren<UIScrollablePanel>();
            mainPanel.autoLayout = true;
            mainPanel.autoLayoutDirection = LayoutDirection.Vertical;
            m_uiHelper = new UIHelperExtension(mainPanel);

            TLMUtils.doLog("PrefixDD");

            m_prefixSelector = m_uiHelper.AddDropdownLocalized("TLM_PREFIX", new string[0], -1, onChangePrefix);

            ReloadPrefixOptions();
            TLMUtils.doLog("PrefixDD Panel");
            var ddPanel = m_prefixSelector.GetComponentInParent<UIPanel>();
            ConfigComponentPanel(m_prefixSelector);


            TLMUtils.doLog("SubPanel");
            TLMUtils.createUIElement(out UIPanel subpanel, mainPanel.transform, "Subpanel", new Vector4(0, 0, 500, 180));
            subpanel.autoLayout = true;
            subpanel.autoLayoutDirection = LayoutDirection.Vertical;
            subpanel.autoSize = true;
            subpanel.autoFitChildrenVertically = true;
            m_subpanel = new UIHelperExtension(subpanel);

            TLMUtils.doLog("AssetSelector");
            TLMUtils.createUIElement(out m_panelAssetSelector, mainPanel.transform, "AssetSelector", new Vector4(0, 0, 0, 0.0001f));
            m_assetSelectorWindow = TLMUtils.createElement(ImplClassChildren, m_panelAssetSelector.transform).GetComponent<TLMAssetSelectorWindowPrefixTab<T>>();

            m_subpanel.self.isVisible = false;
            m_assetSelectorWindow.mainPanel.isVisible = false;

            TLMUtils.doLog("Name");
            m_prefixName = m_subpanel.AddTextField(Locale.Get("TLM_PREFIX_NAME"), null, "", onPrefixNameChange);
            ConfigComponentPanel(m_prefixName);

            TLMUtils.doLog("Price");
            m_prefixTicketPrice = m_subpanel.AddTextField(Locale.Get("TLM_TICKET_PRICE_LABEL"), null, "", onTicketChange);
            ConfigComponentPanel(m_prefixTicketPrice);

            TLMUtils.doLog("ColorForModel");
            m_useColorForModel = m_subpanel.AddCheckboxLocale("TLM_USE_PREFIX_COLOR_FOR_VEHICLE", false, onUseColorVehicleChange);
            TLMUtils.LimitWidth(m_useColorForModel.label, 420, true);

            TLMUtils.doLog("ColorSel");
            CreateColorSelector();

            TLMUtils.doLog("Budget");
            TLMUtils.createUIElement(out UIPanel m_budgetPanel, subpanel.transform, "BudgetPanel", new Vector4(0, 0, 460, 180));
            CreateBudgetSliders(m_budgetPanel);
            CreateToggleBudgetButtons(m_budgetPanel);

            TLMUtils.doLog("Palette");
            m_paletteDD = m_subpanel.AddDropdownLocalized("TLM_PALETTE", new string[0], -1, SetPalettePrefix);

            TLMUtils.doLog("PaletteDD Panel");
            var palettePanel = m_paletteDD.GetComponentInParent<UIPanel>();
            ConfigComponentPanel(m_paletteDD);
            reloadPalettes();
            TLMPaletteOptionsTab.onPaletteReloaded += reloadPalettes;

            TLMUtils.doLog("Format");
            m_formatDD = m_subpanel.AddDropdownLocalized("TLM_ICON", TLMLineIconExtension.getDropDownOptions(Locale.Get("TLM_LINE_ICON_ENUM_TT_DEFAULT")), -1, SetFormatPrefix);
            ConfigComponentPanel(m_formatDD);


            GetComponent<UIComponent>().eventVisibilityChanged += (x, y) => forceRefresh();
            TLMConfigWarehouse.eventOnPropertyChanged += OnWarehouseChange;
        }

        private void SetPalettePrefix(int value)
        {
            extension.SetCustomPalette((uint)SelectedPrefix, value == 0 ? null : m_paletteDD.selectedValue);

        }
        private void SetFormatPrefix(int value)
        {
            extension.SetCustomFormat((uint)SelectedPrefix, (TLMLineIcon)Enum.Parse(typeof(TLMLineIcon), value.ToString()));

        }

        private void OnWarehouseChange(TLMConfigWarehouse.ConfigIndex idx, bool? newValueBool, int? newValueInt, string newValueString)
        {
            if (idx == (TransportSystem.toConfigIndex() | TLMConfigWarehouse.ConfigIndex.PREFIX))
            {
                ReloadPrefixOptions();
            }
        }

        private void reloadPalettes()
        {
            string valSel = (m_paletteDD.selectedValue);
            m_paletteDD.items = TLMAutoColorPalettes.paletteList;
            if (!m_paletteDD.items.Contains(valSel))
            {
                valSel = TLMAutoColorPalettes.PALETTE_RANDOM;
            }
            m_paletteDD.selectedIndex = m_paletteDD.items.ToList().IndexOf(valSel);
            m_paletteDD.items[0] = "-------------------";
        }

        private void CreateToggleBudgetButtons(UIPanel reference)
        {
            TLMUtils.createUIElement(out m_enableBudgetPerHour, reference.transform);
            m_enableBudgetPerHour.relativePosition = new Vector3(reference.width - 65f, 0f);
            m_enableBudgetPerHour.textScale = 0.6f;
            m_enableBudgetPerHour.width = 40;
            m_enableBudgetPerHour.height = 40;
            m_enableBudgetPerHour.tooltip = Locale.Get("TLM_USE_PER_PERIOD_BUDGET");
            TLMUtils.initButton(m_enableBudgetPerHour, true, "ButtonMenu");
            m_enableBudgetPerHour.name = "EnableBudgetPerHour";
            m_enableBudgetPerHour.isVisible = true;
            m_enableBudgetPerHour.eventClick += (component, eventParam) =>
            {
                IBudgetableExtension bte;
                uint idx;
                var tsdRef = TransportSystem;
                bte = TLMLineUtils.getExtensionFromTransportSystemDefinition(ref tsdRef);
                idx = (uint)SelectedPrefix;

                uint[] saveData = bte.GetBudgetsMultiplier(idx);
                uint[] newSaveData = new uint[8];
                for (int i = 0; i < 8; i++)
                {
                    newSaveData[i] = saveData[0];
                }
                bte.SetBudgetMultiplier(idx, newSaveData);
                updateSliders();
            };

            var icon = m_enableBudgetPerHour.AddUIComponent<UISprite>();
            icon.relativePosition = new Vector3(2, 2);
            icon.atlas = TLMController.taTLM;
            icon.width = 36;
            icon.height = 36;
            icon.spriteName = "PerHourIcon";


            TLMUtils.createUIElement(out m_disableBudgetPerHour, reference.transform);
            m_disableBudgetPerHour.relativePosition = new Vector3(reference.width - 65f, 0f);
            m_disableBudgetPerHour.textScale = 0.6f;
            m_disableBudgetPerHour.width = 40;
            m_disableBudgetPerHour.height = 40;
            m_disableBudgetPerHour.tooltip = Locale.Get("TLM_USE_SINGLE_BUDGET");
            TLMUtils.initButton(m_disableBudgetPerHour, true, "ButtonMenu");
            m_disableBudgetPerHour.name = "DisableBudgetPerHour";
            m_disableBudgetPerHour.isVisible = true;
            m_disableBudgetPerHour.eventClick += (component, eventParam) =>
            {
                IBudgetableExtension bte;
                uint idx;
                var tsdRef = TransportSystem;
                bte = TLMLineUtils.getExtensionFromTransportSystemDefinition(ref tsdRef);
                idx = (uint)SelectedPrefix;

                uint[] saveData = bte.GetBudgetsMultiplier(idx);
                uint[] newSaveData = new uint[] { saveData[0] };
                bte.SetBudgetMultiplier(idx, newSaveData);

                updateSliders();
            };

            icon = m_disableBudgetPerHour.AddUIComponent<UISprite>();
            icon.relativePosition = new Vector3(2, 2);
            icon.atlas = TLMController.taTLM;
            icon.width = 36;
            icon.height = 36;
            icon.spriteName = "24hLineIcon";
        }

        private void CreateBudgetSliders(UIPanel reference)
        {
            TLMUtils.createUIElement(out m_lineBudgetSlidersTitle, reference.transform);
            m_lineBudgetSlidersTitle.autoSize = false;
            m_lineBudgetSlidersTitle.relativePosition = new Vector3(0f, 0f);
            m_lineBudgetSlidersTitle.width = 400f;
            m_lineBudgetSlidersTitle.height = 36f;
            m_lineBudgetSlidersTitle.textScale = 0.9f;
            m_lineBudgetSlidersTitle.textAlignment = UIHorizontalAlignment.Center;
            m_lineBudgetSlidersTitle.name = "LineBudgetSlidersTitle";
            m_lineBudgetSlidersTitle.font = UIHelperExtension.defaultFontCheckbox;
            m_lineBudgetSlidersTitle.wordWrap = true;

            var uiHelper = new UIHelperExtension(reference);

            for (int i = 0; i < m_budgetSliders.Length; i++)
            {
                m_budgetSliders[i] = GenerateVerticalBudgetMultiplierField(uiHelper, i);
            }
        }

        private UISlider GenerateVerticalBudgetMultiplierField(UIHelperExtension uiHelper, int idx)
        {
            UISlider bugdetSlider = (UISlider)uiHelper.AddSlider(Locale.Get("TLM_BUDGET_MULTIPLIER_LABEL"), 0f, 5, 0.05f, -1,
                (x) =>
                {

                });
            UILabel budgetSliderLabel = bugdetSlider.transform.parent.GetComponentInChildren<UILabel>();
            UIPanel budgetSliderPanel = bugdetSlider.GetComponentInParent<UIPanel>();

            budgetSliderPanel.relativePosition = new Vector2(45 * idx + 15, 50);
            budgetSliderPanel.width = 40;
            budgetSliderPanel.height = 160;
            bugdetSlider.zOrder = 0;
            budgetSliderPanel.autoLayout = true;

            bugdetSlider.size = new Vector2(40, 100);
            bugdetSlider.scrollWheelAmount = 0;
            bugdetSlider.orientation = UIOrientation.Vertical;
            bugdetSlider.clipChildren = true;
            bugdetSlider.thumbOffset = new Vector2(0, -100);
            bugdetSlider.color = Color.black;

            bugdetSlider.thumbObject.width = 40;
            bugdetSlider.thumbObject.height = 200;
            ((UISprite)bugdetSlider.thumbObject).spriteName = "ScrollbarThumb";
            ((UISprite)bugdetSlider.thumbObject).color = new Color32(1, 140, 46, 255);

            budgetSliderLabel.textScale = 0.5f;
            budgetSliderLabel.autoSize = false;
            budgetSliderLabel.wordWrap = true;
            budgetSliderLabel.pivot = UIPivotPoint.TopCenter;
            budgetSliderLabel.textAlignment = UIHorizontalAlignment.Center;
            budgetSliderLabel.text = string.Format(" x{0:0.00}", 0);
            budgetSliderLabel.prefix = Locale.Get("TLM_BUDGET_MULTIPLIER_PERIOD_LABEL", idx);
            budgetSliderLabel.width = 40;
            budgetSliderLabel.font = UIHelperExtension.defaultFontCheckbox;

            var idx_loc = idx;
            bugdetSlider.eventValueChanged += delegate (UIComponent c, float val)
            {
                budgetSliderLabel.text = string.Format(" x{0:0.00}", val);
                setBudgetHour(val, idx_loc);
            };

            return bugdetSlider;
        }

        private void ConfigComponentPanel(UIComponent reference)
        {
            reference.GetComponentInParent<UIPanel>().autoLayoutDirection = LayoutDirection.Horizontal;
            reference.GetComponentInParent<UIPanel>().wrapLayout = false;
            reference.GetComponentInParent<UIPanel>().autoLayout = false;
            reference.GetComponentInParent<UIPanel>().height = 40;
            TLMUtils.createUIElement(out UIPanel labelContainer, reference.parent.transform, "lblContainer",new Vector4(0,0, 240, reference.height));
            labelContainer.zOrder = 0;
            UILabel lbl = reference.parent.GetComponentInChildren<UILabel>();
            lbl.transform.SetParent(labelContainer.transform);
            lbl.textAlignment = UIHorizontalAlignment.Center;
            lbl.minimumSize = new Vector2(240, reference.height);
            TLMUtils.LimitWidth(lbl, 240);
            lbl.verticalAlignment = UIVerticalAlignment.Middle;
            lbl.pivot = UIPivotPoint.TopCenter;
            lbl.relativePosition = new Vector3(0, lbl.relativePosition.y);
            reference.relativePosition = new Vector3(240, 0);
        }

        private void ReloadPrefixOptions()
        {
            int selIdx = m_prefixSelector.selectedIndex;
            m_prefixSelector.items = TLMUtils.getStringOptionsForPrefix(Singleton<T>.instance.GetTSD().toConfigIndex(), true, true, false);
            m_prefixSelector.selectedIndex = selIdx;
        }

        private void CreateColorSelector()
        {
            TLMUtils.createUIElement(out UIPanel panelColorSelector, m_subpanel.self.transform, "ColorSelector", new Vector4(500, 60, 0, 0));
            panelColorSelector.autoLayout = true;
            panelColorSelector.autoLayoutDirection = LayoutDirection.Horizontal;
            panelColorSelector.autoLayoutPadding = new RectOffset(3, 3, 0, 0);
            panelColorSelector.autoFitChildrenHorizontally = true;
            panelColorSelector.autoFitChildrenVertically = true;

            TLMUtils.createUIElement(out UILabel lbl, panelColorSelector.transform, "PrefixColorLabel", new Vector4(5, 12, 250, 40));
            TLMUtils.LimitWidth(lbl, 250, true);
            lbl.localeID = "TLM_PREFIX_COLOR_LABEL";
            lbl.verticalAlignment = UIVerticalAlignment.Middle;
            lbl.font = UIHelperExtension.defaultFontCheckbox;

            m_prefixColor = KlyteUtils.CreateColorField(panelColorSelector);
            m_prefixColor.eventSelectedColorChanged += onChangePrefixColor;

            TLMUtils.createUIElement(out UIButton resetColor, panelColorSelector.transform, "PrefixColorReset", new Vector4(290, 0, 0, 0));
            TLMUtils.initButton(resetColor, false, "ButtonMenu");
            TLMUtils.LimitWidth(resetColor, 200);
            resetColor.textPadding = new RectOffset(5, 5, 5, 2);
            resetColor.autoSize = true;
            resetColor.localeID = "TLM_RESET_COLOR";
            resetColor.eventClick += onResetColor;
        }
        #endregion
        #region Actions
        private void onResetColor(UIComponent component, UIMouseEventParameter eventParam)
        {
            m_prefixColor.selectedColor = Color.clear;
        }
        private void onChangePrefixColor(UIComponent component, Color selectedColor)
        {
            if (!isChanging && m_prefixSelector.selectedIndex >= 0)
            {
                extension.SetColor((uint)m_prefixSelector.selectedIndex, selectedColor);
            }
            eventOnColorChange?.Invoke(selectedColor);
        }
        private void onUseColorVehicleChange(bool val)
        {
            if (!isChanging && m_prefixSelector.selectedIndex >= 0)
            {
                extension.SetUsingColorForModel((uint)m_prefixSelector.selectedIndex, val);
            }
        }
        private void onPrefixNameChange(string val)
        {
            if (!isChanging && m_prefixSelector.selectedIndex >= 0)
            {
                extension.SetName((uint)m_prefixSelector.selectedIndex, val);
                ReloadPrefixOptions();
            }
        }
        private void onTicketChange(string val)
        {
            if (!isChanging && m_prefixSelector.selectedIndex >= 0 && uint.TryParse(val, out uint intVal))
            {
                extension.SetTicketPrice((uint)m_prefixSelector.selectedIndex, intVal);
            }
        }
        private void setBudgetHour(float x, int selectedHourIndex)
        {
            if (!isChanging && m_prefixSelector.selectedIndex >= 0)
            {
                uint idx = (uint)SelectedPrefix;
                ushort val = (ushort)(x * 100 + 0.5f);
                IBudgetableExtension bte;
                uint[] saveData;

                var tsdRef = TransportSystem;
                bte = TLMLineUtils.getExtensionFromTransportSystemDefinition(ref tsdRef);
                saveData = bte.GetBudgetsMultiplier(idx);
                if (selectedHourIndex >= saveData.Length || saveData[selectedHourIndex] == val)
                {
                    return;
                }
                saveData[selectedHourIndex] = val;
                bte.SetBudgetMultiplier(idx, saveData);
            }
        }
        #endregion

        #region On Selection Changed
        private bool isChanging = false;

        public event OnItemSelectedChanged onSelectionChanged;
        public event OnItemSelectedChanged onLineUpdated;

        private void onChangePrefix(int sel)
        {
            isChanging = true;
            m_subpanel.self.isVisible = sel >= 0;
            m_assetSelectorWindow.mainPanel.isVisible = sel >= 0;
            if (sel >= 0)
            {
                m_prefixColor.selectedColor = extension.GetColor((uint)sel);
                m_useColorForModel.isChecked = extension.IsUsingColorForModel((uint)sel);
                m_prefixTicketPrice.text = extension.GetTicketPrice((uint)sel).ToString();
                m_prefixName.text = extension.GetName((uint)sel) ?? "";
                m_paletteDD.selectedIndex = Math.Max(0, m_paletteDD.items.ToList().IndexOf(extension.GetCustomPalette((uint)sel)));
                m_formatDD.selectedIndex = Math.Max(0, (int)extension.GetCustomFormat((uint)sel));
                updateSliders();

                eventOnPrefixChange?.Invoke(sel);
            }
            isChanging = false;
        }
        private void updateSliders()
        {
            if (TLMSingleton.isIPTLoaded)
            {
                m_lineBudgetSlidersTitle.parent.isVisible = false;
                return;
            }


            TLMConfigWarehouse.ConfigIndex transportType = TransportSystem.toConfigIndex();

            uint[] multipliers;
            IBudgetableExtension bte;
            uint idx;

            var tsdRef = TransportSystem;

            idx = (uint)SelectedPrefix;
            bte = TLMLineUtils.getExtensionFromTransportSystemDefinition(ref tsdRef);
            multipliers = bte.GetBudgetsMultiplier(idx);

            m_lineBudgetSlidersTitle.text = string.Format(Locale.Get("TLM_BUDGET_MULTIPLIER_TITLE_PREFIX"), idx > 0 ? TLMUtils.getStringFromNumber(TLMUtils.getStringOptionsForPrefix(transportType), (int)idx + 1) : Locale.Get("TLM_UNPREFIXED"), TLMConfigWarehouse.getNameForTransportType(tsdRef.toConfigIndex()));


            bool budgetPerHourEnabled = multipliers.Length == 8;
            m_disableBudgetPerHour.isVisible = budgetPerHourEnabled;
            m_enableBudgetPerHour.isVisible = !budgetPerHourEnabled && tsdRef.hasVehicles();
            for (int i = 0; i < m_budgetSliders.Length; i++)
            {
                UILabel budgetSliderLabel = m_budgetSliders[i].transform.parent.GetComponentInChildren<UILabel>();
                if (i == 0)
                {
                    if (multipliers.Length == 1)
                    {
                        budgetSliderLabel.prefix = Locale.Get("TLM_BUDGET_MULTIPLIER_PERIOD_LABEL_ALL");
                    }
                    else
                    {
                        budgetSliderLabel.prefix = Locale.Get("TLM_BUDGET_MULTIPLIER_PERIOD_LABEL", 0);
                    }
                }
                else
                {
                    m_budgetSliders[i].isEnabled = budgetPerHourEnabled;
                    m_budgetSliders[i].parent.isVisible = budgetPerHourEnabled;
                }

                if (i < multipliers.Length)
                {
                    m_budgetSliders[i].value = multipliers[i] / 100f;
                }
            }
        }
        public void forceRefresh()
        {
            onChangePrefix(SelectedPrefix);
        }
        #endregion

        private void Start()
        {
        }

        private void Update()
        {

        }

    }

    internal sealed class TLMTabControllerPrefixListNorBus : TLMTabControllerPrefixList<TLMSysDefNorBus> { }
    internal sealed class TLMTabControllerPrefixListNorTrm : TLMTabControllerPrefixList<TLMSysDefNorTrm> { }
    internal sealed class TLMTabControllerPrefixListNorMnr : TLMTabControllerPrefixList<TLMSysDefNorMnr> { }
    internal sealed class TLMTabControllerPrefixListNorMet : TLMTabControllerPrefixList<TLMSysDefNorMet> { }
    internal sealed class TLMTabControllerPrefixListNorTrn : TLMTabControllerPrefixList<TLMSysDefNorTrn> { }
    internal sealed class TLMTabControllerPrefixListNorFer : TLMTabControllerPrefixList<TLMSysDefNorFer> { }
    internal sealed class TLMTabControllerPrefixListNorBlp : TLMTabControllerPrefixList<TLMSysDefNorBlp> { }
    internal sealed class TLMTabControllerPrefixListNorShp : TLMTabControllerPrefixList<TLMSysDefNorShp> { }
    internal sealed class TLMTabControllerPrefixListNorPln : TLMTabControllerPrefixList<TLMSysDefNorPln> { }
    internal sealed class TLMTabControllerPrefixListTouBus : TLMTabControllerPrefixList<TLMSysDefTouBus> { }

    public delegate void OnPrefixLoad(int prefix);
    public delegate void OnColorChanged(Color newColor);
}
