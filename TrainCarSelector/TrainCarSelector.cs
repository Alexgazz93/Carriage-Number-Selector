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

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
                return;
            _ui = new TrainCarUIPanel();
            _ui.Initialize();
        }

        public override void OnLevelUnloading()
        {
            if (_ui != null)
            {
                _ui.Destroy();
                _ui = null;
            }
            TrainCarLogic.ClearMemory();
        }
    }

    internal class CountRefresher : MonoBehaviour
    {
        internal TrainCarUIPanel panel;
        private ushort _lastId;

        void LateUpdate()
        {
            if (panel == null) return;
            ushort id = panel.GetSelectedLeadVehicle();
            if (id != _lastId)
            {
                _lastId = id;
                panel.OnSelectionChanged(id);
            }
        }
    }

    public class TrainCarUIPanel
    {
        private UIPanel _container;
        private UIButton _addButton;
        private UIButton _removeButton;
        private UIButton _resetButton;
        private UIButton _applyLineButton;
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
            _container.size = new Vector2(168, 25);
            _container.relativePosition = new Vector3(parent.width - 178, 40);

            _resetButton      = CreateButton(_container, "\u21A9", new Vector3(  0, 0), 25);
            _removeButton     = CreateButton(_container, "\u2212", new Vector3( 28, 0), 25);
            _countField       = CreateTextField(_container,        new Vector3( 56, 1));
            _addButton        = CreateButton(_container, "+",      new Vector3( 98, 0), 25);
            _applyLineButton  = CreateButton(_container, "\u2225", new Vector3(126, 0), 25);
            _resetButton.textScale = 0.85f;

            _addButton.eventClick       += OnAddClicked;
            _removeButton.eventClick    += OnRemoveClicked;
            _resetButton.eventClick     += OnResetClicked;
            _applyLineButton.eventClick += OnApplyLineClicked;
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

        private void OnCountSubmitted(UIComponent c, string text)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            int target;
            if (!int.TryParse(text, out target)) return;
            TrainCarLogic.SetWagonCount(id, target);
            RefreshCount();
        }

        private void OnApplyLineClicked(UIComponent c, UIMouseEventParameter e)
        {
            ushort id = GetSelectedLeadVehicle();
            if (id == 0) return;
            int count = TrainCarLogic.GetTotalCount(id);
            TrainCarLogic.ApplyWagonCountToLine(id, count);
        }

        private void OnPanelVisibilityChanged(UIComponent c, bool visible)
        {
            if (!visible)
                _container.isVisible = false;
        }

        internal void OnSelectionChanged(ushort leadId)
        {
            bool supported = leadId != 0 && TrainCarLogic.IsRailVehicle(leadId);
            _container.isVisible = supported;
            if (supported) RefreshCount();
        }

        private void RefreshCount()
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

        public void Destroy()
        {
            if (_container != null)
                UnityEngine.Object.Destroy(_container.gameObject);
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
            List<VehicleInfo> origInfos;
            if (!_originalChain.TryGetValue(leadId, out origInfos) || origInfos.Count < 2)
                return;

            List<Quaternion> origRots;
            _originalRotations.TryGetValue(leadId, out origRots);

            int safety = MaxTotal;
            while (GetTotalCount(leadId) > MinTotal && safety-- > 0)
                RemoveWagon(leadId);

            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            Quaternion currentLeadRot = buf[leadId].m_frame0.m_rotation;

            for (int i = 1; i < origInfos.Count - 1; i++)
            {
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

        /// <summary>Applique le nombre de wagons a tous les trains du meme type 
        /// sur la meme ligne de transport que le train donne.</summary>
        public static void ApplyWagonCountToLine(ushort leadId, int targetCount)
        {
            if (!IsRailVehicle(leadId)) return;
            
            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            ushort lineId = buf[leadId].m_transportLine;
            if (lineId == 0) return;

            VehicleInfo targetInfo = buf[leadId].Info;
            TransportLine line = TransportManager.instance.m_lines.m_buffer[lineId];
            
            ushort vehicleId = line.m_vehicles.m_head;
            int safety = SafetyLimit * 10;
            
            while (vehicleId != 0 && safety-- > 0)
            {
                ushort lead = GetLeadVehicle(vehicleId);
                if (buf[lead].Info == targetInfo)
                    SetWagonCount(lead, targetCount);
                
                vehicleId = buf[vehicleId].m_nextLineVehicle;
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
            return newId;
        }

        public static void RemoveWagon(ushort leadId)
        {
            if (!IsRailVehicle(leadId)) return;
            List<ushort> chain = GetChain(leadId);
            if (chain.Count <= MinTotal) return;
            RememberOriginalState(leadId);

            Vehicle[] buf = VehicleManager.instance.m_vehicles.m_buffer;
            ushort removeId = chain[chain.Count - 2];
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
            if (buf[removeId].m_path != 0u)
            {
                Singleton<PathManager>.instance.ReleasePath(buf[removeId].m_path);
                buf[removeId].m_path = 0u;
            }
            buf[removeId].m_transportLine = 0;
            VehicleManager.instance.ReleaseVehicle(removeId);
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
    }
}
