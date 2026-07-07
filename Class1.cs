using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace TrainCarSelector
{
    public class TrainCarSelectorMod : IUserMod
    {
        private static readonly Dictionary<string, string> _names =
            new Dictionary<string, string>()
            {
                { "en", "Carriage Number Selector" },
                { "fr", "Carriage Number Selector" },
                { "de", "Carriage Number Selector" },
                { "es", "Carriage Number Selector" },
            };

        private static readonly Dictionary<string, string> _descriptions =
            new Dictionary<string, string>()
            {
                { "en", "Add or remove carriages from train, metro, monorail and tram via the vehicle info panel." },
                { "fr", "Ajouter ou retirer des wagons des trains, metros, monorails et tramways depuis la bulle d'info." },
                { "de", "Waggons eines Zuges, U-Bahn, Einschienenbahn oder Strassenbahn hinzufuegen/entfernen." },
                { "es", "Selector de vagones: anadir o quitar vagones del tren, metro, monorriel y tranvia." },
            };

        public string Name { get { return GetLocalized(_names); } }
        public string Description { get { return GetLocalized(_descriptions); } }

        private static string GetLocalized(Dictionary<string, string> dict)
        {
            string lang = GetGameLanguageCode();
            string value;
            if (dict.TryGetValue(lang, out value))
                return value;
            return dict["en"];
        }

        private static string GetGameLanguageCode()
        {
            try
            {
                switch (Application.systemLanguage)
                {
                    case SystemLanguage.French:  return "fr";
                    case SystemLanguage.German:  return "de";
                    case SystemLanguage.Spanish: return "es";
                    default: return "en";
                }
            }
            catch { return "en"; }
        }
    }

    public class TrainCarSelectorLoading : LoadingExtensionBase
    {
        private TrainCarUIPanel _ui;
        private TrainCarWagonPanel _wagonPanel;

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
                return;
            _ui = new TrainCarUIPanel();
            _ui.Initialize();
            _wagonPanel = new TrainCarWagonPanel();
            _wagonPanel.Initialize(_ui);
            
            CountRefresher refresher = _ui.GetRefresher();
            if (refresher != null)
                refresher.wagonPanel = _wagonPanel;
        }

        public override void OnLevelUnloading()
        {
            if (_ui != null)
            {
                _ui.Destroy();
                _ui = null;
            }
            if (_wagonPanel != null)
            {
                _wagonPanel.Destroy();
                _wagonPanel = null;
            }
            TrainCarLogic.ClearMemory();
        }
    }

    internal class CountRefresher : MonoBehaviour
    {
        internal TrainCarUIPanel panel;
        internal TrainCarWagonPanel wagonPanel;
        private ushort _lastLeadId;
        private ushort _lastSelectedVehicleId;
        private int _orphanCleanupCounter;

        void LateUpdate()
        {
            if (panel == null) return;

            if (++_orphanCleanupCounter >= 30)
            {
                _orphanCleanupCounter = 0;
                TrainCarLogic.CleanupAllRailOrphans();
            }

            ushort leadId = panel.GetSelectedLeadVehicle();
            ushort selectedVehicleId = panel.GetSelectedVehicle();
            if (leadId != _lastLeadId || selectedVehicleId != _lastSelectedVehicleId)
            {
                _lastLeadId = leadId;
                _lastSelectedVehicleId = selectedVehicleId;
                panel.OnSelectionChanged(leadId);
                if (wagonPanel != null)
                    wagonPanel.UpdateWagons(leadId);
            }
        }
    }

    public class TrainCarUIPanel
    {
        private UIPanel _container;
        private UIButton _addButton;
        private UIButton _removeButton;
        private UIButton _resetButton;
        private UIButton _equalButton;
        private UIButton _reverseButton;
        private UITextField _countField;
        private CountRefresher _refresher;

        public void Initialize()
        {
            PublicTransportVehicleWorldInfoPanel infoPanel = UIView.library
                .Get<PublicTransportVehicleWorldInfoPanel>(
                    typeof(PublicTransportVehicleWorldInfoPanel).Name);
            if (infoPanel == null) return;
            UIPanel parent = infoPanel.component as UIPanel;
            if (parent == null) return;

            _container = parent.AddUIComponent<UIPanel>();
            _container.name = "TrainCarSelectorPanel";
            _container.size = new Vector2(188, 25);
            _container.relativePosition = new Vector3(parent.width - 198, 40);

            _resetButton   = CreateButton(_container, "\u21A9", new Vector3(  0, 0), 25);
            _removeButton  = CreateButton(_container, "\u2212", new Vector3( 28, 0), 25);
            _countField    = CreateTextField(_container,        new Vector3( 56, 1));
            _addButton     = CreateButton(_container, "+",      new Vector3( 98, 0), 25);
            _equalButton   = CreateButton(_container, "=",      new Vector3(126, 0), 25);
            _reverseButton = CreateButton(_container, "\u2194",  new Vector3(154, 0), 25);
            _resetButton.textScale = 0.85f;

            _addButton.eventClick       += OnAddClicked;
            _removeButton.eventClick    += OnRemoveClicked;
            _resetButton.eventClick     += OnResetClicked;
            _equalButton.eventClick     += OnEqualClicked;
            _reverseButton.eventClick   += OnReverseClicked;
            _countField.eventTextSubmitted += OnCountSubmitted;
            infoPanel.component.eventVisibilityChanged += OnPanelVisibilityChanged;

            _refresher = _container.gameObject.AddComponent<CountRefresher>();
            _refresher.panel = this;
        }

        private void OnAddClicked(UIComponent c, UIMouseEventParameter e)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            TrainCarLogic.AddWagon(id);
            RefreshCount();
        }

        private void OnRemoveClicked(UIComponent c, UIMouseEventParameter e)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            TrainCarLogic.RemoveWagon(id);
            RefreshCount();
        }

        private void OnResetClicked(UIComponent c, UIMouseEventParameter e)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            TrainCarLogic.ResetToOriginal(id);
            RefreshCount();
        }

        private void OnEqualClicked(UIComponent c, UIMouseEventParameter e)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            TrainCarLogic.ApplyExactConfigToLineType(id);
            RefreshCount();
        }

        private void OnReverseClicked(UIComponent c, UIMouseEventParameter e)
        {
            ushort id = GetSelectedVehicle();
            if (id == 0) return;
            TrainCarLogic.ReverseVehicle(id);
        }

        private void OnCountSubmitted(UIComponent c, string text)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            int target;
            if (!int.TryParse(text, out target)) return;
            TrainCarLogic.SetWagonCount(id, target);
            RefreshCount();
        }

        private void OnPanelVisibilityChanged(UIComponent c, bool visible)
        {
            try
            {
                if (!visible)
                {
                    _container.isVisible = false;
                    if (_refresher != null && _refresher.wagonPanel != null)
                    {
                        _refresher.wagonPanel.ClearPanel();
                    }
                }
            }
            catch { }
        }

        internal void OnSelectionChanged(ushort leadId)
        {
            bool supported = leadId != 0 && TrainCarLogic.IsRailVehicle(leadId);
            _container.isVisible = supported;
            if (supported) RefreshCount();
        }

        internal void RefreshCount()
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0 || _countField == null) return;
            _countField.text = TrainCarLogic.GetTotalCount(id).ToString();
        }

        internal ushort GetSelectedLeadVehicle()
        {
            InstanceID instance = WorldInfoPanel.GetCurrentInstanceID();
            if (instance.Type != InstanceType.Vehicle) return 0;
            return TrainCarLogic.GetLeadVehicle(instance.Vehicle);
        }

        internal ushort GetSelectedVehicle()
        {
            InstanceID instance = WorldInfoPanel.GetCurrentInstanceID();
            if (instance.Type != InstanceType.Vehicle) return 0;
            ushort id = instance.Vehicle;
            if (id == 0) return 0;
            if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return 0;
            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            if (buf.Length <= id) return 0;
            if ((buf[id].m_flags & Vehicle.Flags.Created) == 0) return 0;
            return id;
        }

        private static UIButton CreateButton(UIPanel parent, string text, Vector3 pos, float width)
        {
            UIButton btn = parent.AddUIComponent<UIButton>();
            btn.text = text;
            btn.size = new Vector2(width, 25);
            btn.relativePosition = pos;
            btn.normalBgSprite  = "ButtonMenu";
            btn.hoveredBgSprite = "ButtonMenuHovered";
            btn.pressedBgSprite = "ButtonMenuPressed";
            btn.textScale = 1.0f;
            btn.textColor = Color.white;
            return btn;
        }

        private static UITextField CreateTextField(UIPanel parent, Vector3 pos)
        {
            UITextField field = parent.AddUIComponent<UITextField>();
            field.size = new Vector2(39, 22);
            field.relativePosition = pos;
            field.builtinKeyNavigation = true;
            field.isInteractive = true;
            field.readOnly = false;
            field.numericalOnly = true;
            field.allowFloats = false;
            field.allowNegative = false;
            field.maxLength = 2;
            field.text = "0";
            field.textScale = 0.8f;
            field.textColor = Color.white;
            field.selectionSprite  = "EmptySprite";
            field.normalBgSprite   = "TextFieldPanel";
            field.hoveredBgSprite  = "TextFieldPanelHovered";
            field.focusedBgSprite  = "TextFieldPanel";
            field.horizontalAlignment = UIHorizontalAlignment.Center;
            field.padding = new RectOffset(0, 0, 4, 0);
            return field;
        }

        internal CountRefresher GetRefresher()
        {
            return _refresher;
        }

        public void Destroy()
        {
            if (_container != null)
                UnityEngine.Object.Destroy(_container.gameObject);
        }
    }

    public class TrainCarWagonPanel
    {
        private UIPanel _panel;
        private TrainCarUIPanel _parentPanel;
        private ushort _lastLeadId;

        public void Initialize(TrainCarUIPanel parentPanel)
        {
            _parentPanel = parentPanel;
            EnsurePanelExists();
        }

        private void EnsurePanelExists()
        {
            if (_panel != null) return;

            PublicTransportVehicleWorldInfoPanel infoPanel = UIView.library
                .Get<PublicTransportVehicleWorldInfoPanel>(
                    typeof(PublicTransportVehicleWorldInfoPanel).Name);
            if (infoPanel == null) return;

            UIPanel parent = infoPanel.component as UIPanel;
            if (parent == null) return;

            _panel = parent.AddUIComponent<UIPanel>();
            _panel.name = "TrainCarWagonPanel";
            _panel.relativePosition = new Vector3(parent.width + 10, 40);
            _panel.backgroundSprite = "GenericPanel";
            _panel.color = new Color32(200, 200, 200, 240);
            _panel.autoLayout = true;
            _panel.autoLayoutDirection = LayoutDirection.Vertical;
            _panel.autoLayoutStart = LayoutStart.TopLeft;
            _panel.autoLayoutPadding = new RectOffset(2, 2, 2, 2);
            _panel.isVisible = false;
        }

        public void UpdateWagons(ushort leadId)
        {
            try
            {
                EnsurePanelExists();
                if (_panel == null) return;

                _lastLeadId = leadId;

                // Nettoyer les enfants de manière sécurisée (Destroy est différé en fin de frame :
                // on capture le nombre d'enfants une seule fois pour éviter une boucle infinie)
                int childCount = _panel.transform.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    try
                    {
                        Transform child = _panel.transform.GetChild(i);
                        if (child != null)
                            UnityEngine.Object.Destroy(child.gameObject);
                    }
                    catch { }
                }

                if (leadId == 0)
                {
                    _panel.isVisible = false;
                    return;
                }

                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null)
                {
                    _panel.isVisible = false;
                    return;
                }

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= leadId)
                {
                    _panel.isVisible = false;
                    return;
                }

                // Vérifier que le véhicule existe vraiment
                if ((buf[leadId].m_flags & Vehicle.Flags.Created) == 0)
                {
                    _panel.isVisible = false;
                    return;
                }

                if (!TrainCarLogic.IsRailVehicle(leadId))
                {
                    _panel.isVisible = false;
                    return;
                }

                ushort selectedVehicleId = _parentPanel.GetSelectedVehicle();
                if (selectedVehicleId == 0)
                {
                    _panel.isVisible = false;
                    return;
                }

                List<ushort> selectedChain = TrainCarLogic.GetChain(leadId);
                if (selectedChain.Count <= 2)
                {
                    _panel.isVisible = false;
                    return;
                }

                int selectedIndex = selectedChain.IndexOf(selectedVehicleId);
                if (selectedIndex <= 0 || selectedIndex >= selectedChain.Count - 1)
                {
                    // Liste inutile pour la tête et la queue (et si la sélection n'est pas dans la chaîne)
                    _panel.isVisible = false;
                    return;
                }

                VehicleInfo leadInfo = buf[leadId].Info;
                if (leadInfo == null)
                {
                    _panel.isVisible = false;
                    return;
                }

                List<VehicleInfo> wagons = TrainCarLogic.GetAvailableWagons(leadInfo);
                if (wagons.Count == 0)
                {
                    _panel.isVisible = false;
                    return;
                }

                for (int wagonIndex = 0; wagonIndex < wagons.Count; wagonIndex++)
                {
                    VehicleInfo wagon = wagons[wagonIndex];
                    try
                    {
                        UIButton btn = _panel.AddUIComponent<UIButton>();
                        btn.size = new Vector2(150, 25);
                        btn.text = "Wagon " + (wagonIndex + 1);
                        btn.normalBgSprite = "ButtonMenu";
                        btn.hoveredBgSprite = "ButtonMenuHovered";
                        btn.pressedBgSprite = "ButtonMenuPressed";
                        btn.textScale = 0.7f;
                        btn.textColor = Color.white;
                        btn.wordWrap = true;

                        VehicleInfo wagonRef = wagon;
                        ushort safeLeadId = leadId;
                        btn.eventClick += (c, e) =>
                        {
                            try
                            {
                                // Vérifier à nouveau que le véhicule existe avant de remplacer
                                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null)
                                    return;

                                Vehicle[] clickBuf = VehicleManager.instance.m_vehicles.m_buffer;
                                if (safeLeadId >= clickBuf.Length)
                                    return;

                                if ((clickBuf[safeLeadId].m_flags & Vehicle.Flags.Created) != 0)
                                {
                                    ushort clickedSelectedVehicleId = _parentPanel.GetSelectedVehicle();
                                    if (clickedSelectedVehicleId == 0)
                                        return;

                                    TrainCarLogic.ReplaceWagon(clickedSelectedVehicleId, wagonRef);
                                    _parentPanel.RefreshCount();
                                }
                            }
                            catch { }
                        };
                    }
                    catch { }
                }

                _panel.width = 150;
                _panel.height = wagons.Count * 27 + 4;
                _panel.isVisible = true;
            }
            catch { }
        }

        public void HidePanel()
        {
            try
            {
                if (_panel != null)
                {
                    _panel.isVisible = false;
                }
            }
            catch { }
        }

        public void ClearPanel()
        {
            try
            {
                if (_panel != null)
                {
                    // Détruire tous les enfants en inverse pour éviter les index invalides
                    int count = _panel.transform.childCount;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        Transform child = _panel.transform.GetChild(i);
                        if (child != null)
                            UnityEngine.Object.Destroy(child.gameObject);
                    }
                    _panel.isVisible = false;
                    _lastLeadId = 0;
                }
            }
            catch { }
        }

        public void Destroy()
        {
            try
            {
                if (_panel != null)
                {
                    _panel.isVisible = false;
                    UnityEngine.Object.Destroy(_panel.gameObject);
                }
            }
            catch { }
        }
    }

    public static class TrainCarLogic
    {
        private const int SafetyLimit = 100;
        private const int MinTotal = 2;
        private const int MaxTotal = 64;
        private const float FallbackSpacing = 30f;

        private static readonly Dictionary<ushort, VehicleInfo> _wagonMemory =
            new Dictionary<ushort, VehicleInfo>();
        private static readonly Dictionary<ushort, int> _originalCount =
            new Dictionary<ushort, int>();
        private static readonly Dictionary<ushort, List<VehicleInfo>> _originalChain =
            new Dictionary<ushort, List<VehicleInfo>>();
        private static readonly Dictionary<ushort, List<Quaternion>> _originalRotations =
            new Dictionary<ushort, List<Quaternion>>();

        public static void ClearMemory()
        {
            _wagonMemory.Clear();
            _originalCount.Clear();
            _originalChain.Clear();
            _originalRotations.Clear();
        }

        public static bool IsRailVehicle(ushort vehicleId)
        {
            VehicleInfo info = VehicleManager.instance.m_vehicles.m_buffer[vehicleId].Info;
            if (info == null)
                return false;

            return IsRailPublicTransportInfo(info);
        }

        private static bool IsRailPublicTransportInfo(VehicleInfo info)
        {
            if (info == null || info.m_class.m_service != ItemClass.Service.PublicTransport)
                return false;

            ItemClass.SubService sub = info.m_class.m_subService;
            return sub == ItemClass.SubService.PublicTransportTrain
                || sub == ItemClass.SubService.PublicTransportMetro
                || sub == ItemClass.SubService.PublicTransportMonorail
                || sub == ItemClass.SubService.PublicTransportTram;
        }

        private static void RememberOriginalState(ushort leadId)
        {
            if (_originalCount.ContainsKey(leadId)) return;

            List<ushort> chain = GetChain(leadId);
            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            _originalCount[leadId] = chain.Count;

            Quaternion leadRotInv = Quaternion.Inverse(buf[chain[0]].m_frame0.m_rotation);
            List<VehicleInfo> infos = new List<VehicleInfo>();
            List<Quaternion> rots = new List<Quaternion>();
            for (int i = 0; i < chain.Count; i++)
            {
                infos.Add(buf[chain[i]].Info);
                rots.Add(leadRotInv * buf[chain[i]].m_frame0.m_rotation);
            }
            _originalChain[leadId] = infos;
            _originalRotations[leadId] = rots;
        }

        public static void ResetToOriginal(ushort leadId)
        {
            try
            {
                List<VehicleInfo> origInfos;
                if (!_originalChain.TryGetValue(leadId, out origInfos) || origInfos.Count < 2)
                    return;

                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;

                List<Quaternion> origRots;
                _originalRotations.TryGetValue(leadId, out origRots);

                int safety = MaxTotal;
                while (GetTotalCount(leadId) > MinTotal && safety-- > 0)
                    RemoveWagon(leadId);

                // Sécurité anti wagons orphelins : si le nettoyage n'a pas réussi à ramener
                // le train à sa taille minimale (échec silencieux de RemoveWagon), on ne
                // reconstruit pas par-dessus les wagons restants pour éviter les doublons.
                List<ushort> cleanedChain = GetChain(leadId);
                if (cleanedChain.Count != MinTotal) return;

                if (cleanedChain.Count == 2)
                {
                    Vehicle[] cleanedBuf = VehicleManager.instance.m_vehicles.m_buffer;
                    if (cleanedBuf.Length <= leadId) return;
                    if (cleanedChain[0] != leadId) return;
                    if (cleanedChain[1] == 0) return;
                    if (cleanedBuf[cleanedChain[0]].m_trailingVehicle != cleanedChain[1]) return;
                    if (cleanedBuf[cleanedChain[1]].m_leadingVehicle != cleanedChain[0]) return;
                    if ((cleanedBuf[cleanedChain[0]].m_flags & Vehicle.Flags.Created) == 0) return;
                    if ((cleanedBuf[cleanedChain[1]].m_flags & Vehicle.Flags.Created) == 0) return;
                }

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= leadId) return;
                Quaternion currentLeadRot = buf[leadId].m_frame0.m_rotation;

                for (int i = 1; i < origInfos.Count - 1; i++)
                {
                    if (origInfos[i] == null) continue;
                    ushort newId = InsertWagonBeforeTail(leadId, origInfos[i]);
                    if (newId != 0 && origRots != null && i < origRots.Count)
                        ApplyRotationToFrames(buf, newId, currentLeadRot * origRots[i]);
                }

                if (origRots != null)
                {
                    int tailOrigIdx = origInfos.Count - 1;
                    if (tailOrigIdx < origRots.Count)
                    {
                        List<ushort> finalChain = GetChain(leadId);
                        if (finalChain.Count > 1)
                        {
                            ushort tailId = finalChain[finalChain.Count - 1];
                            ApplyRotationToFrames(buf, tailId, currentLeadRot * origRots[tailOrigIdx]);
                        }
                    }
                }

                if (origInfos.Count >= 3)
                    _wagonMemory[leadId] = origInfos[origInfos.Count / 2];
                else if (origInfos.Count >= 2)
                    _wagonMemory[leadId] = origInfos[1];
            }
            catch { }
        }

        private static void RememberWagon(ushort leadId, VehicleInfo info)
        {
            if (info != null) _wagonMemory[leadId] = info;
        }

        private static VehicleInfo GetRememberedWagon(ushort leadId)
        {
            VehicleInfo info;
            if (_wagonMemory.TryGetValue(leadId, out info) && info != null)
                return info;

            List<ushort> chain = GetChain(leadId);
            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;

            if (chain.Count >= 3)
                info = buf[chain[chain.Count / 2]].Info;
            else if (chain.Count >= 2)
                info = buf[chain[1]].Info;

            if (info != null) _wagonMemory[leadId] = info;
            return info;
        }

        public static ushort GetLeadVehicle(ushort vehicleId)
        {
            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            ushort cur = vehicleId;
            int limit = SafetyLimit;
            while (buf[cur].m_leadingVehicle != 0 && limit-- > 0)
                cur = buf[cur].m_leadingVehicle;
            return cur;
        }

        public static List<ushort> GetChain(ushort leadId)
        {
            List<ushort> chain = new List<ushort>();
            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            ushort cur = leadId;
            int limit = SafetyLimit;
            while (cur != 0 && limit-- > 0)
            {
                chain.Add(cur);
                cur = buf[cur].m_trailingVehicle;
            }
            return chain;
        }

        public static int GetTotalCount(ushort leadId)
        {
            return GetChain(leadId).Count;
        }

        public static void SetWagonCount(ushort leadId, int target)
        {
            if (!IsRailVehicle(leadId)) return;
            target = Mathf.Clamp(target, MinTotal, MaxTotal);
            int current = GetTotalCount(leadId);
            int safety = MaxTotal;
            while (current < target && safety-- > 0)
            {
                AddWagon(leadId);
                int next = GetTotalCount(leadId);
                if (next == current) break;
                current = next;
            }
            while (current > target && safety-- > 0)
            {
                RemoveWagon(leadId);
                int next = GetTotalCount(leadId);
                if (next == current) break;
                current = next;
            }
        }

        public static void AddWagon(ushort leadId)
        {
            if (!IsRailVehicle(leadId)) return;
            List<ushort> chain = GetChain(leadId);
            if (chain.Count >= MaxTotal) return;
            RememberOriginalState(leadId);

            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            if (chain.Count >= 3)
                RememberWagon(leadId, buf[chain[chain.Count / 2]].Info);
            else if (chain.Count >= 2)
                RememberWagon(leadId, buf[chain[1]].Info);

            VehicleInfo wagonInfo = GetRememberedWagon(leadId);
            if (wagonInfo != null) InsertWagonBeforeTail(leadId, wagonInfo);
        }

        private static ushort InsertWagonBeforeTail(ushort leadId, VehicleInfo wagonInfo)
        {
            List<ushort> chain = GetChain(leadId);
            if (chain.Count < 2) return 0;

            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            int tailIdx = chain.Count - 1;
            int insertAfterIdx = tailIdx - 1;
            ushort insertAfterId = chain[insertAfterIdx];
            ushort tailId = chain[tailIdx];

            Vector3 insertAfterPos = buf[insertAfterId].GetLastFramePosition();
            Vector3 tailPos = buf[tailId].GetLastFramePosition();
            float spacing = Vector3.Distance(insertAfterPos, tailPos);
            if (spacing < 1f) spacing = FallbackSpacing;

            Vector3 forward = (insertAfterPos - tailPos).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = -(buf[insertAfterId].m_frame0.m_rotation * Vector3.forward);

            Vector3 newPos = insertAfterPos - forward * (spacing * 0.5f);
            Vector3 shiftedTailPos = insertAfterPos - forward * (spacing + spacing * 0.5f);

            ushort newId;
            if (!VehicleManager.instance.CreateVehicle(out newId,
                    ref SimulationManager.instance.m_randomizer,
                    wagonInfo, newPos,
                    TransferManager.TransferReason.None, false, false))
                return 0;

            if (buf[newId].m_path != 0u)
            {
                Singleton<PathManager>.instance.ReleasePath(buf[newId].m_path);
                buf[newId].m_path = 0u;
            }
            buf[newId].m_transportLine = 0;
            buf[newId].m_blockCounter = 0;
            buf[newId].m_waitCounter = 0;
            buf[newId].m_sourceBuilding = 0;
            buf[newId].m_targetBuilding = 0;

            Vector3 newOffset = newPos - insertAfterPos;
            CopyFramesWithOffset(buf, insertAfterId, newId, newOffset);

            Quaternion midRot = Quaternion.Slerp(
                buf[insertAfterId].m_frame0.m_rotation,
                buf[tailId].m_frame0.m_rotation, 0.5f);
            ApplyRotationToFrames(buf, newId, midRot);

            Vector4 newOff4 = new Vector4(newOffset.x, newOffset.y, newOffset.z, 0f);
            buf[newId].m_targetPos0 = buf[insertAfterId].m_targetPos0 + newOff4;
            buf[newId].m_targetPos1 = buf[insertAfterId].m_targetPos1 + newOff4;
            buf[newId].m_targetPos2 = buf[insertAfterId].m_targetPos2 + newOff4;
            buf[newId].m_targetPos3 = buf[insertAfterId].m_targetPos3 + newOff4;

            Vector3 tailOffset = shiftedTailPos - tailPos;
            ShiftFrames(buf, tailId, tailOffset);
            Vector4 tailOff4 = new Vector4(tailOffset.x, tailOffset.y, tailOffset.z, 0f);
            buf[tailId].m_targetPos0 += tailOff4;
            buf[tailId].m_targetPos1 += tailOff4;
            buf[tailId].m_targetPos2 += tailOff4;
            buf[tailId].m_targetPos3 += tailOff4;

            buf[newId].m_flags = buf[insertAfterId].m_flags;
            buf[newId].m_transferType = buf[insertAfterId].m_transferType;

            buf[insertAfterId].m_trailingVehicle = newId;
            buf[newId].m_leadingVehicle = insertAfterId;
            buf[newId].m_trailingVehicle = tailId;
            buf[tailId].m_leadingVehicle = newId;

            StabilizeVehicleMotion(buf, newId);
            return newId;
        }

        public static void RemoveWagon(ushort leadId)
        {
            try
            {
                if (!IsRailVehicle(leadId)) return;
                List<ushort> chain = GetChain(leadId);
                if (chain.Count <= MinTotal) return;
                RememberOriginalState(leadId);

                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= leadId) return;

                ushort removeId = chain[chain.Count - 2];
                if (buf.Length <= removeId) return;
                if (removeId == leadId) return;
                if (buf[removeId].m_leadingVehicle == 0) return;
                if (buf[removeId].m_trailingVehicle == 0) return;

                TransferSelectionAndFollowToLeadIfNeeded(removeId, leadId);

                RememberWagon(leadId, buf[removeId].Info);

                ushort prevId = buf[removeId].m_leadingVehicle;
                ushort nextId = buf[removeId].m_trailingVehicle;

                if (prevId != 0) buf[prevId].m_trailingVehicle = nextId;
                if (nextId != 0) buf[nextId].m_leadingVehicle = prevId;

                if (prevId != 0 && nextId != 0)
                {
                    Vector3 prevPos = buf[prevId].GetLastFramePosition();
                    Vector3 nextPos = buf[nextId].GetLastFramePosition();
                    Vector3 forward = (prevPos - nextPos).normalized;

                    if (forward.sqrMagnitude < 0.0001f)
                        forward = -(buf[prevId].m_frame0.m_rotation * Vector3.forward);

                    ushort beforePrev = buf[prevId].m_leadingVehicle;
                    float spacing = beforePrev != 0
                        ? Vector3.Distance(buf[beforePrev].GetLastFramePosition(), prevPos)
                        : FallbackSpacing;

                    Vector3 tailShift = (prevPos - forward * spacing) - nextPos;
                    ShiftFrames(buf, nextId, tailShift);
                    Vector4 tailOff4 = new Vector4(tailShift.x, tailShift.y, tailShift.z, 0f);
                    buf[nextId].m_targetPos0 += tailOff4;
                    buf[nextId].m_targetPos1 += tailOff4;
                    buf[nextId].m_targetPos2 += tailOff4;
                    buf[nextId].m_targetPos3 += tailOff4;
                }

                buf[removeId].m_leadingVehicle = 0;
                buf[removeId].m_trailingVehicle = 0;

                // Faire disparaître immédiatement le wagon retiré pour éviter tout
                // état visuel orphelin/désorienté avant ReleaseVehicle.
                Vehicle.Frame rf0 = buf[removeId].m_frame0;
                Vehicle.Frame rf1 = buf[removeId].m_frame1;
                Vehicle.Frame rf2 = buf[removeId].m_frame2;
                Vehicle.Frame rf3 = buf[removeId].m_frame3;
                rf0.m_position = Vector3.zero;
                rf1.m_position = Vector3.zero;
                rf2.m_position = Vector3.zero;
                rf3.m_position = Vector3.zero;
                buf[removeId].m_frame0 = rf0;
                buf[removeId].m_frame1 = rf1;
                buf[removeId].m_frame2 = rf2;
                buf[removeId].m_frame3 = rf3;
                buf[removeId].m_targetPos0 = Vector4.zero;
                buf[removeId].m_targetPos1 = Vector4.zero;
                buf[removeId].m_targetPos2 = Vector4.zero;
                buf[removeId].m_targetPos3 = Vector4.zero;

                if (buf[removeId].m_path != 0u)
                {
                    Singleton<PathManager>.instance.ReleasePath(buf[removeId].m_path);
                    buf[removeId].m_path = 0u;
                }
                buf[removeId].m_transportLine = 0;
                VehicleManager.instance.ReleaseVehicle(removeId);
            }
            catch { }
        }

        private static void TransferSelectionAndFollowToLeadIfNeeded(ushort oldVehicleId, ushort leadId)
        {
            try
            {
                if (oldVehicleId == 0 || leadId == 0 || oldVehicleId == leadId) return;

                InstanceID oldSelection = WorldInfoPanel.GetCurrentInstanceID();
                if (oldSelection.Type == InstanceType.Vehicle && oldSelection.Vehicle == oldVehicleId)
                {
                    InstanceID oldId = InstanceID.Empty;
                    oldId.Vehicle = oldVehicleId;
                    InstanceID newId = InstanceID.Empty;
                    newId.Vehicle = leadId;
                    WorldInfoPanel.ChangeInstanceID(oldId, newId);
                }

                CameraController camera = ToolsModifierControl.cameraController;
                if (camera != null)
                {
                    InstanceID camTarget = camera.GetTarget();
                    if (camTarget.Type == InstanceType.Vehicle && camTarget.Vehicle == oldVehicleId)
                    {
                        InstanceID oldId = InstanceID.Empty;
                        oldId.Vehicle = oldVehicleId;
                        InstanceID newId = InstanceID.Empty;
                        newId.Vehicle = leadId;
                        camera.ChangeTarget(oldId, newId);
                    }
                }
            }
            catch { }
        }

        private static void ApplyRotationToFrames(Vehicle[] buf, ushort id, Quaternion rotation)
        {
            Vehicle.Frame f0 = buf[id].m_frame0;
            Vehicle.Frame f1 = buf[id].m_frame1;
            Vehicle.Frame f2 = buf[id].m_frame2;
            Vehicle.Frame f3 = buf[id].m_frame3;
            f0.m_rotation = rotation;
            f1.m_rotation = rotation;
            f2.m_rotation = rotation;
            f3.m_rotation = rotation;
            buf[id].m_frame0 = f0;
            buf[id].m_frame1 = f1;
            buf[id].m_frame2 = f2;
            buf[id].m_frame3 = f3;
        }

        private static void StabilizeVehicleMotion(Vehicle[] buf, ushort id)
        {
            if (id == 0 || id >= buf.Length) return;

            Vehicle.Frame f0 = buf[id].m_frame0;
            Vehicle.Frame f1 = buf[id].m_frame1;
            Vehicle.Frame f2 = buf[id].m_frame2;
            Vehicle.Frame f3 = buf[id].m_frame3;

            f0.m_swayVelocity = Vector3.zero;
            f1.m_swayVelocity = Vector3.zero;
            f2.m_swayVelocity = Vector3.zero;
            f3.m_swayVelocity = Vector3.zero;

            f0.m_swayPosition = Vector3.zero;
            f1.m_swayPosition = Vector3.zero;
            f2.m_swayPosition = Vector3.zero;
            f3.m_swayPosition = Vector3.zero;

            f0.m_angleVelocity = 0f;
            f1.m_angleVelocity = 0f;
            f2.m_angleVelocity = 0f;
            f3.m_angleVelocity = 0f;

            buf[id].m_frame0 = f0;
            buf[id].m_frame1 = f1;
            buf[id].m_frame2 = f2;
            buf[id].m_frame3 = f3;
        }

        private static float GetWagonSpacing(VehicleInfo info)
        {
            if (info != null)
            {
                try
                {
                    float length = info.m_generatedInfo.m_size.z;
                    if (length > 1f)
                        return Mathf.Clamp(length, 6f, 80f);
                }
                catch { }

                try
                {
                    if (info.m_mesh != null)
                    {
                        float meshLength = info.m_mesh.bounds.size.z;
                        if (meshLength > 1f)
                            return Mathf.Clamp(meshLength, 6f, 80f);
                    }
                }
                catch { }
            }

            return FallbackSpacing;
        }

        private static void CleanupDetachedTrailerOrphans(VehicleInfo leadInfo)
        {
            CleanupAllRailOrphans();
        }

        private static bool IsExplicitTrailerOfLead(VehicleInfo leadInfo, VehicleInfo wagonInfo)
        {
            if (leadInfo == null || wagonInfo == null) return false;
            if (leadInfo.m_trailers == null || leadInfo.m_trailers.Length == 0) return false;

            for (int i = 0; i < leadInfo.m_trailers.Length; i++)
            {
                VehicleInfo.VehicleTrailer trailer = leadInfo.m_trailers[i];
                if (trailer.m_info == wagonInfo)
                    return true;
            }

            return false;
        }

        public static void CleanupAllRailOrphans()
        {
            try
            {
                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                uint vehicleCount = VehicleManager.instance.m_vehicles.m_size;

                ushort selectedVehicleId = 0;
                InstanceID current = WorldInfoPanel.GetCurrentInstanceID();
                if (current.Type == InstanceType.Vehicle)
                    selectedVehicleId = current.Vehicle;

                for (ushort vehicleId = 1; vehicleId < vehicleCount; vehicleId++)
                {
                    if ((buf[vehicleId].m_flags & Vehicle.Flags.Created) == 0) continue;
                    if (vehicleId == selectedVehicleId) continue;

                    VehicleInfo info = buf[vehicleId].Info;
                    if (!IsRailPublicTransportInfo(info)) continue;

                    if (buf[vehicleId].m_leadingVehicle != 0) continue;
                    if (buf[vehicleId].m_trailingVehicle != 0) continue;
                    if (buf[vehicleId].m_transportLine != 0) continue;
                    if (buf[vehicleId].m_path != 0u) continue;
                    if (buf[vehicleId].m_sourceBuilding != 0) continue;
                    if (buf[vehicleId].m_targetBuilding != 0) continue;

                    if (buf[vehicleId].m_path != 0u)
                    {
                        Singleton<PathManager>.instance.ReleasePath(buf[vehicleId].m_path);
                        buf[vehicleId].m_path = 0u;
                    }

                    Vehicle.Frame of0 = buf[vehicleId].m_frame0;
                    Vehicle.Frame of1 = buf[vehicleId].m_frame1;
                    Vehicle.Frame of2 = buf[vehicleId].m_frame2;
                    Vehicle.Frame of3 = buf[vehicleId].m_frame3;
                    of0.m_position = Vector3.zero;
                    of1.m_position = Vector3.zero;
                    of2.m_position = Vector3.zero;
                    of3.m_position = Vector3.zero;
                    buf[vehicleId].m_frame0 = of0;
                    buf[vehicleId].m_frame1 = of1;
                    buf[vehicleId].m_frame2 = of2;
                    buf[vehicleId].m_frame3 = of3;
                    buf[vehicleId].m_targetPos0 = Vector4.zero;
                    buf[vehicleId].m_targetPos1 = Vector4.zero;
                    buf[vehicleId].m_targetPos2 = Vector4.zero;
                    buf[vehicleId].m_targetPos3 = Vector4.zero;

                    buf[vehicleId].m_leadingVehicle = 0;
                    buf[vehicleId].m_trailingVehicle = 0;
                    buf[vehicleId].m_transportLine = 0;
                    VehicleManager.instance.ReleaseVehicle(vehicleId);
                }
            }
            catch { }
        }

        private static void CopyFramesWithOffset(Vehicle[] buf, ushort srcId, ushort dstId, Vector3 offset)
        {
            Vehicle.Frame f0 = buf[srcId].m_frame0;
            Vehicle.Frame f1 = buf[srcId].m_frame1;
            Vehicle.Frame f2 = buf[srcId].m_frame2;
            Vehicle.Frame f3 = buf[srcId].m_frame3;
            f0.m_position += offset;
            f1.m_position += offset;
            f2.m_position += offset;
            f3.m_position += offset;
            buf[dstId].m_frame0 = f0;
            buf[dstId].m_frame1 = f1;
            buf[dstId].m_frame2 = f2;
            buf[dstId].m_frame3 = f3;
        }

        private static void ShiftFrames(Vehicle[] buf, ushort id, Vector3 offset)
        {
            Vehicle.Frame f0 = buf[id].m_frame0;
            Vehicle.Frame f1 = buf[id].m_frame1;
            Vehicle.Frame f2 = buf[id].m_frame2;
            Vehicle.Frame f3 = buf[id].m_frame3;
            f0.m_position += offset;
            f1.m_position += offset;
            f2.m_position += offset;
            f3.m_position += offset;
            buf[id].m_frame0 = f0;
            buf[id].m_frame1 = f1;
            buf[id].m_frame2 = f2;
            buf[id].m_frame3 = f3;
        }

        public static void ApplyExactConfigToLineType(ushort sourceLeadId)
        {
            try
            {
                if (!IsRailVehicle(sourceLeadId)) return;
                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= sourceLeadId) return;
                if ((buf[sourceLeadId].m_flags & Vehicle.Flags.Created) == 0) return;

                ushort transportLine = buf[sourceLeadId].m_transportLine;
                if (transportLine == 0) return;

                VehicleInfo leadInfo = buf[sourceLeadId].Info;
                if (leadInfo == null) return;

                List<ushort> sourceChain = GetChain(sourceLeadId);
                if (sourceChain.Count < 2) return;

                // Capturer la configuration exacte du train source : le modèle de chaque
                // wagon du milieu, et l'état inversé (orientation) de chaque élément de la chaîne.
                List<VehicleInfo> middleInfos = new List<VehicleInfo>();
                for (int i = 1; i < sourceChain.Count - 1; i++)
                    middleInfos.Add(buf[sourceChain[i]].Info);

                List<bool> invertedStates = new List<bool>();
                for (int i = 0; i < sourceChain.Count; i++)
                    invertedStates.Add((buf[sourceChain[i]].m_flags & Vehicle.Flags.Inverted) != 0);

                // Collecter d'abord les trains cibles (têtes de chaîne) avant toute
                // modification : créer/retirer des wagons pendant le parcours du buffer
                // pourrait fausser l'itération ou retraiter un train déjà modifié.
                List<ushort> targetLeadIds = new List<ushort>();
                uint vehicleCount = VehicleManager.instance.m_vehicles.m_size;
                for (ushort vehicleId = 1; vehicleId < vehicleCount; vehicleId++)
                {
                    if ((buf[vehicleId].m_flags & Vehicle.Flags.Created) == 0) continue;
                    if (buf[vehicleId].m_leadingVehicle != 0) continue;
                    if (buf[vehicleId].m_trailingVehicle == 0) continue;
                    if (vehicleId == sourceLeadId) continue;
                    if (buf[vehicleId].m_transportLine != transportLine) continue;
                    if (buf[vehicleId].Info != leadInfo) continue;
                    targetLeadIds.Add(vehicleId);
                }

                foreach (ushort targetLeadId in targetLeadIds)
                    ApplyExactConfigToTrain(targetLeadId, middleInfos, invertedStates);
            }
            catch { }
        }

        private static void ApplyExactConfigToTrain(ushort targetLeadId, List<VehicleInfo> middleInfos, List<bool> invertedStates)
        {
            try
            {
                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;
                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= targetLeadId) return;
                if ((buf[targetLeadId].m_flags & Vehicle.Flags.Created) == 0) return;
                if (buf[targetLeadId].m_leadingVehicle != 0) return;

                RememberOriginalState(targetLeadId);

                int safety = MaxTotal;
                while (GetTotalCount(targetLeadId) > MinTotal && safety-- > 0)
                    RemoveWagon(targetLeadId);

                // Sécurité anti wagons orphelins : si le nettoyage n'a pas ramené le train
                // à sa taille minimale, on n'insère pas de nouveaux wagons par-dessus.
                List<ushort> cleanedChain = GetChain(targetLeadId);
                if (cleanedChain.Count != MinTotal) return;

                for (int i = 0; i < middleInfos.Count; i++)
                {
                    if (middleInfos[i] == null) continue;
                    InsertWagonBeforeTail(targetLeadId, middleInfos[i]);
                }

                buf = VehicleManager.instance.m_vehicles.m_buffer;
                List<ushort> targetChain = GetChain(targetLeadId);
                for (int i = 0; i < targetChain.Count && i < invertedStates.Count; i++)
                {
                    ushort id = targetChain[i];
                    if (invertedStates[i])
                        buf[id].m_flags |= Vehicle.Flags.Inverted;
                    else
                        buf[id].m_flags &= ~Vehicle.Flags.Inverted;
                }

                if (middleInfos.Count >= 1)
                    RememberWagon(targetLeadId, middleInfos[middleInfos.Count / 2]);

                CleanupDetachedTrailerOrphans(buf[targetLeadId].Info);
            }
            catch { }
        }

        public static void ReverseVehicle(ushort vehicleId)
        {
            try
            {
                if (!IsRailVehicle(vehicleId)) return;
                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= vehicleId) return;
                if ((buf[vehicleId].m_flags & Vehicle.Flags.Created) == 0) return;

                // Bascule le flag persistant d'inversion. m_frame0..3 sont recalculés par la
                // simulation à chaque tick à partir du chemin du véhicule : modifier la rotation
                // directement ne tenait pas (le wagon reprenait son sens d'origine). Le flag
                // Inverted est lu par l'IA du véhicule et reste stable tant qu'il n'est pas rebasculé.
                buf[vehicleId].m_flags ^= Vehicle.Flags.Inverted;
            }
            catch { }
        }

        public static List<VehicleInfo> GetAvailableWagons(VehicleInfo leadInfo)
        {
            List<VehicleInfo> result = new List<VehicleInfo>();
            if (leadInfo == null) return result;

            // Récupérer les trailers officiels définis pour cette locomotive
            if (leadInfo.m_trailers != null && leadInfo.m_trailers.Length > 0)
            {
                foreach (var trailerInfo in leadInfo.m_trailers)
                {
                    VehicleInfo trailer = trailerInfo.m_info;
                    if (trailer != null && !result.Contains(trailer))
                        result.Add(trailer);
                }
            }

            // Si pas de trailers trouvés, chercher les wagons du même type (fallback)
            if (result.Count == 0)
            {
                for (uint i = 0; i < PrefabCollection<VehicleInfo>.LoadedCount(); i++)
                {
                    VehicleInfo vehicleInfo = PrefabCollection<VehicleInfo>.GetLoaded(i);
                    if (vehicleInfo == null) continue;

                    // Vérifier si c'est du même type de transport
                    if (vehicleInfo.m_class.m_service != ItemClass.Service.PublicTransport) continue;
                    if (vehicleInfo.m_class.m_subService != leadInfo.m_class.m_subService) continue;

                    // Exclure la locomotive elle-même
                    if (vehicleInfo == leadInfo) continue;

                    // Ajouter si pas déjà présent
                    if (!result.Contains(vehicleInfo))
                        result.Add(vehicleInfo);
                }
            }

            return result;
        }

        public static void AddSpecificWagon(ushort leadId, VehicleInfo wagonInfo)
        {
            if (!IsRailVehicle(leadId)) return;
            if (wagonInfo == null) return;

            RememberOriginalState(leadId);
            RememberWagon(leadId, wagonInfo);
            InsertWagonBeforeTail(leadId, wagonInfo);
        }

        public static void ReplaceWagon(ushort vehicleId, VehicleInfo wagonInfo)
        {
            try
            {
                if (!IsRailVehicle(vehicleId)) return;
                if (wagonInfo == null) return;
                if (VehicleManager.instance == null || VehicleManager.instance.m_vehicles.m_buffer == null) return;

                Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
                if (buf.Length <= vehicleId) return;
                if ((buf[vehicleId].m_flags & Vehicle.Flags.Created) == 0) return;

                ushort leadId = GetLeadVehicle(vehicleId);
                if (leadId == 0) return;

                List<ushort> chain = GetChain(leadId);
                if (chain.Count < 2) return;

                int selectedIndex = -1;
                for (int i = 0; i < chain.Count; i++)
                {
                    if (chain[i] == vehicleId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                if (selectedIndex < 0) return;

                // Ne pas remplacer la tête ni la queue via ce bouton, pour éviter
                // les cassures de chaîne et les wagons orphelins.
                if (selectedIndex == 0 || selectedIndex == chain.Count - 1) return;

                RememberOriginalState(leadId);

                TransferSelectionAndFollowToLeadIfNeeded(vehicleId, leadId);

                Vehicle.Frame oldF0 = buf[vehicleId].m_frame0;
                Vehicle.Frame oldF1 = buf[vehicleId].m_frame1;
                Vehicle.Frame oldF2 = buf[vehicleId].m_frame2;
                Vehicle.Frame oldF3 = buf[vehicleId].m_frame3;
                Vector4 oldT0 = buf[vehicleId].m_targetPos0;
                Vector4 oldT1 = buf[vehicleId].m_targetPos1;
                Vector4 oldT2 = buf[vehicleId].m_targetPos2;
                Vector4 oldT3 = buf[vehicleId].m_targetPos3;
                Vehicle.Flags oldFlags = buf[vehicleId].m_flags;
                byte oldTransferType = buf[vehicleId].m_transferType;

                buf[vehicleId].Info = wagonInfo;
                buf[vehicleId].m_frame0 = oldF0;
                buf[vehicleId].m_frame1 = oldF1;
                buf[vehicleId].m_frame2 = oldF2;
                buf[vehicleId].m_frame3 = oldF3;
                buf[vehicleId].m_targetPos0 = oldT0;
                buf[vehicleId].m_targetPos1 = oldT1;
                buf[vehicleId].m_targetPos2 = oldT2;
                buf[vehicleId].m_targetPos3 = oldT3;
                buf[vehicleId].m_flags = oldFlags;
                buf[vehicleId].m_transferType = oldTransferType;

                StabilizeVehicleMotion(buf, vehicleId);
                RememberWagon(leadId, wagonInfo);
            }
            catch { }
        }
    }
}
