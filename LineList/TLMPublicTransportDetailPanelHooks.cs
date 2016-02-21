﻿using ColossalFramework;
using ColossalFramework.UI;
using Klyte.TransportLinesManager.Extensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using TLMCW = Klyte.TransportLinesManager.TLMConfigWarehouse;

namespace Klyte.TransportLinesManager.LineList
{
    class TLMPublicTransportDetailPanelHooks : Redirector
    {

        private static TLMPublicTransportDetailPanelHooks _instance;
        public static TLMPublicTransportDetailPanelHooks instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TLMPublicTransportDetailPanelHooks();
                }
                return _instance;
            }
        }

        private void RefreshData(bool colors, bool visible) { }
        private void OnTabChanged(UIComponent c, int idx) { }
        private void Awake() { }
        private void RefreshLines() { }

        #region Hooking
        private static Dictionary<MethodInfo, RedirectCallsState> redirects = new Dictionary<MethodInfo, RedirectCallsState>();
        public void EnableHooks()
        {
            if (redirects.Count != 0)
            {
                DisableHooks();
            }
            TLMUtils.doLog("Loading TLMPublicTransportDetailPanelHooks Hooks!");
            AddRedirect(typeof(PublicTransportDetailPanel), typeof(TLMPublicTransportDetailPanelHooks).GetMethod("RefreshLines", allFlags), ref redirects);
            AddRedirect(typeof(PublicTransportDetailPanel), typeof(TLMPublicTransportDetailPanelHooks).GetMethod("Awake", allFlags), ref redirects);
            AddRedirect(typeof(PublicTransportDetailPanel), typeof(TLMPublicTransportDetailPanelHooks).GetMethod("OnTabChanged", allFlags), ref redirects);
            AddRedirect(typeof(PublicTransportLineInfo), typeof(TLMPublicTransportDetailPanelHooks).GetMethod("RefreshData", allFlags), ref redirects);

            TLMUtils.doLog("Inverse TLMPublicTransportDetailPanelHooks Hooks!");
            AddRedirect(typeof(TLMPublicTransportDetailPanel), typeof(PublicTransportDetailPanel).GetMethod("NaturalCompare", allFlags), ref redirects);

            TLMUtils.doLog("Swap TLMPublicTransportDetailPanelHooks Hooks!");
            var go = GameObject.Find("UIView").GetComponentInChildren<PublicTransportDetailPanel>().gameObject;
            GameObject.Destroy(go.GetComponent<PublicTransportDetailPanel>());
            go.AddComponent<TLMPublicTransportDetailPanel>();


        }

        public void DisableHooks()
        {
            foreach (var kvp in redirects)
            {
                RedirectionHelper.RevertRedirect(kvp.Key, kvp.Value);
            }
            redirects.Clear();
        }
        #endregion
    }
    class TLMPublicTransportDetailPanel : UICustomControl
    {
        private enum LineSortCriterion
        {
            DEFAULT,
            NAME,
            STOP,
            VEHICLE,
            PASSENGER
        }

        private static readonly string kLineTemplate = "LineTemplate";

        private int m_LastLineCount;

        private bool m_Ready;

        private bool m_LinesUpdated;

        private bool[] m_ToggleAllState;

        private LineSortCriterion m_LastSortCriterion;

        private UITabstrip m_Strip;

        private UIComponent m_BusLinesContainer;

        private UIComponent m_TramLinesContainer;

        private UIComponent m_MetroLinesContainer;

        private UIComponent m_TrainLinesContainer;

        private UIComponent m_LowBusLinesContainer;
        private UIComponent m_HighBusLinesContainer;

        private UIComponent m_SurfaceMetroLinesContainer;
        private UIComponent m_BulletTrainLinesContainer;

        private UIComponent m_ShipLinesContainer;

        private UICheckBox m_ToggleAll;

        private static int CompareNames(UIComponent left, UIComponent right)
        {
            TLMPublicTransportLineInfo component = left.GetComponent<TLMPublicTransportLineInfo>();
            TLMPublicTransportLineInfo component2 = right.GetComponent<TLMPublicTransportLineInfo>();
            return NaturalCompare(component.lineName, component2.lineName);
        }

        private static int CompareStops(UIComponent left, UIComponent right)
        {
            TLMPublicTransportLineInfo component = left.GetComponent<TLMPublicTransportLineInfo>();
            TLMPublicTransportLineInfo component2 = right.GetComponent<TLMPublicTransportLineInfo>();
            return NaturalCompare(component.stopCounts, component2.stopCounts);
        }

        private static int CompareVehicles(UIComponent left, UIComponent right)
        {
            TLMPublicTransportLineInfo component = left.GetComponent<TLMPublicTransportLineInfo>();
            TLMPublicTransportLineInfo component2 = right.GetComponent<TLMPublicTransportLineInfo>();
            return NaturalCompare(component.vehicleCounts, component2.vehicleCounts);
        }

        private static int ComparePassengers(UIComponent left, UIComponent right)
        {
            TLMPublicTransportLineInfo component = left.GetComponent<TLMPublicTransportLineInfo>();
            TLMPublicTransportLineInfo component2 = right.GetComponent<TLMPublicTransportLineInfo>();
            return component.passengerCountsInt.CompareTo(component2.passengerCountsInt);
        }
        private static int NaturalCompare(string left, string right)
        {
            return 0;
        }

        private void Awake()
        {
            //this.m_Strip.tab
            enabled = true;

            TLMUtils.clearAllVisibilityEvents(this.GetComponent<UIPanel>());

            this.m_Strip = Find<UITabstrip>("Tabstrip");

            this.m_Strip.relativePosition = new Vector3(13, 45);

            var lowBus = m_Strip.AddTab("");
            var highBus = m_Strip.AddTab("");
            var surfMetro = m_Strip.AddTab("");
            var bulletTrain = m_Strip.AddTab("");
            var ship = m_Strip.AddTab("");
            var bus = m_Strip.tabs[0].GetComponent<UIButton>();
            var tram = m_Strip.tabs[1].GetComponent<UIButton>();
            var metro = m_Strip.tabs[2].GetComponent<UIButton>();
            var train = m_Strip.tabs[3].GetComponent<UIButton>();


            addIcon("LowBus", "LowBusImage", ref lowBus, true, 4, "Low Capacity Bus Lines");
            addIcon("HighBus", "HighBusImage", ref highBus, true, 5, "High Capacity Bus Lines");
            addIcon("BulletTrain", "BulletTrainImage", ref bulletTrain, true, 6, "Bullet Train Lines");
            addIcon("SurfaceMetro", "SurfaceMetroImage", ref surfMetro, true, 7, "Surface Metro Lines");
            addIcon("ShipLine", PublicTransportWorldInfoPanel.GetVehicleTypeIcon(TransportInfo.TransportType.Ship), ref ship, false, 8, "Ship Lines");
            addIcon("Train", PublicTransportWorldInfoPanel.GetVehicleTypeIcon(TransportInfo.TransportType.Train), ref train, false, 3, "PUBLICTRANSPORT_TRAINLINES", true);
            addIcon("Subway", PublicTransportWorldInfoPanel.GetVehicleTypeIcon(TransportInfo.TransportType.Metro), ref metro, false, 2, "PUBLICTRANSPORT_METROLINES", true);
            addIcon("Tram", PublicTransportWorldInfoPanel.GetVehicleTypeIcon(TransportInfo.TransportType.Tram), ref tram, false, 1, "PUBLICTRANSPORT_TRAMLINES", true);
            addIcon("Bus", PublicTransportWorldInfoPanel.GetVehicleTypeIcon(TransportInfo.TransportType.Bus), ref bus, false, 0, "PUBLICTRANSPORT_BUSLINES", true);

            ship.transform.SetSiblingIndex(0);
            bulletTrain.transform.SetSiblingIndex(1);
            train.transform.SetSiblingIndex(2);
            surfMetro.transform.SetSiblingIndex(3);
            metro.transform.SetSiblingIndex(4);
            tram.transform.SetSiblingIndex(5);
            highBus.transform.SetSiblingIndex(6);
            bus.transform.SetSiblingIndex(7);
            lowBus.transform.SetSiblingIndex(8);
            

            tram.isVisible = Singleton<TransportManager>.instance.TransportTypeLoaded(TransportInfo.TransportType.Tram);

            this.m_BusLinesContainer = Find<UIComponent>("BusDetail").Find("Container");
            this.m_TramLinesContainer = Find<UIComponent>("TramDetail").Find("Container");
            this.m_MetroLinesContainer = Find<UIComponent>("MetroDetail").Find("Container");
            this.m_TrainLinesContainer = Find<UIComponent>("TrainDetail").Find("Container");

            m_BusLinesContainer.eventVisibilityChanged += null;
            m_TramLinesContainer.eventVisibilityChanged += null;
            m_MetroLinesContainer.eventVisibilityChanged += null;
            m_TrainLinesContainer.eventVisibilityChanged += null;

            CopyContainerFromBus(4, ref m_LowBusLinesContainer);
            CopyContainerFromBus(5, ref m_HighBusLinesContainer);
            CopyContainerFromBus(6, ref m_SurfaceMetroLinesContainer);
            CopyContainerFromBus(7, ref m_BulletTrainLinesContainer);
            CopyContainerFromBus(8, ref m_ShipLinesContainer);



            RemoveExtraLines(0, ref m_BusLinesContainer);
            RemoveExtraLines(0, ref m_TramLinesContainer);
            RemoveExtraLines(0, ref m_MetroLinesContainer);
            RemoveExtraLines(0, ref m_TrainLinesContainer);
            RemoveExtraLines(0, ref m_LowBusLinesContainer);
            RemoveExtraLines(0, ref m_HighBusLinesContainer);
            RemoveExtraLines(0, ref m_SurfaceMetroLinesContainer);
            RemoveExtraLines(0, ref m_BulletTrainLinesContainer);
            RemoveExtraLines(0, ref m_ShipLinesContainer);

            this.m_ToggleAllState = new bool[this.m_Strip.tabCount];
            this.m_Strip.eventSelectedIndexChanged += null;
            this.m_Strip.eventSelectedIndexChanged += new PropertyChangedEventHandler<int>(this.OnTabChanged);
            this.m_ToggleAll = Find<UICheckBox>("ToggleAll");
            this.m_ToggleAll.eventCheckChanged += new PropertyChangedEventHandler<bool>(this.CheckChangedFunction);
            for (int i = 0; i < this.m_ToggleAllState.Length; i++)
            {
                this.m_ToggleAllState[i] = this.m_ToggleAll.isChecked;
            }
            Find<UIButton>("NameTitle").eventClick += delegate (UIComponent c, UIMouseEventParameter r)
            {
                this.OnNameSort();
            };
            Find<UIButton>("StopsTitle").eventClick += delegate (UIComponent c, UIMouseEventParameter r)
            {
                this.OnStopSort();
            };
            Find<UIButton>("VehiclesTitle").eventClick += delegate (UIComponent c, UIMouseEventParameter r)
            {
                this.OnVehicleSort();
            };
            Find<UIButton>("PassengersTitle").eventClick += delegate (UIComponent c, UIMouseEventParameter r)
            {
                this.OnPassengerSort();
            };
            this.m_LastSortCriterion = LineSortCriterion.DEFAULT;
            this.SetActiveTab(0);
            m_Ready = true;
        }

        private void CopyContainerFromBus(int idx, ref UIComponent item)
        {
            item = GameObject.Instantiate(m_BusLinesContainer.gameObject).GetComponent<UIComponent>();
            item.transform.SetParent(m_Strip.tabContainer.gameObject.transform.GetChild(idx));
            item.name = "Container";
            var scroll = GameObject.Instantiate(Find<UIComponent>("BusDetail").Find("Scrollbar"));
            scroll.transform.SetParent(m_Strip.tabContainer.gameObject.transform.GetChild(idx));
            scroll.name = "Scrollbar";
            item.transform.localPosition = m_BusLinesContainer.transform.localPosition;
            scroll.transform.localPosition = Find<UIComponent>("BusDetail").Find("Scrollbar").transform.localPosition;
            item.GetComponent<UIScrollablePanel>().verticalScrollbar = scroll.GetComponent<UIScrollbar>();
            item.eventVisibilityChanged += null;
        }



        private void addIcon(string namePrefix, string iconName, ref UIButton targetButton, bool alternativeIconAtlas, int tabIdx, string tooltipText, bool isTooltipLocale = false)
        {
            TLMUtils.doLog("addIcon: init " + namePrefix);

            TLMUtils.initButtonFg(targetButton, false, "");

            targetButton.atlas = TLMController.taLineNumber;
            targetButton.width = 40;
            targetButton.height = 40;
            targetButton.name = namePrefix + "Legend";
            TLMUtils.initButtonSameSprite(targetButton, namePrefix + "Icon");
            targetButton.hoveredColor = Color.gray;
            targetButton.focusedColor = Color.green;
            targetButton.eventClick += null;
            targetButton.eventClick += (x, y) =>
           {
               SetActiveTab(tabIdx);
           };
            TLMUtils.doLog("addIcon: pre eventClick");
            TLMUtils.doLog("addIcon: init label icon");
            UILabel icon = targetButton.AddUIComponent<UILabel>();
            if (alternativeIconAtlas)
            {
                icon.atlas = TLMController.taLineNumber;
                icon.width = 27;
                icon.height = 27;
                icon.relativePosition = new Vector3(6f, 6);
            }
            else
            {
                icon.width = 30;
                icon.height = 20;
                icon.relativePosition = new Vector3(5f, 10f);
            }

            if (isTooltipLocale)
            {
                icon.tooltipLocaleID = tooltipText;
            }
            else
            {
                icon.tooltip = tooltipText;
            }

            icon.backgroundSprite = iconName;
            TLMUtils.doLog("addIcon: end");
        }


        private void OnNameSort()
        {
            UIComponent uIComponent = this.m_Strip.tabContainer.components[this.m_Strip.selectedIndex].Find("Container");
            PublicTransportDetailPanel.Quicksort(uIComponent.components, new Comparison<UIComponent>(CompareNames));
            this.m_LastSortCriterion = LineSortCriterion.NAME;
            uIComponent.Invalidate();
        }

        private void OnStopSort()
        {
            UIComponent uIComponent = this.m_Strip.tabContainer.components[this.m_Strip.selectedIndex].Find("Container");
            PublicTransportDetailPanel.Quicksort(uIComponent.components, new Comparison<UIComponent>(CompareStops));
            this.m_LastSortCriterion = LineSortCriterion.STOP;
            uIComponent.Invalidate();
        }

        private void OnVehicleSort()
        {
            UIComponent uIComponent = this.m_Strip.tabContainer.components[this.m_Strip.selectedIndex].Find("Container");
            PublicTransportDetailPanel.Quicksort(uIComponent.components, new Comparison<UIComponent>(CompareVehicles));
            this.m_LastSortCriterion = LineSortCriterion.VEHICLE;
            uIComponent.Invalidate();
        }

        private void OnPassengerSort()
        {
            UIComponent uIComponent = this.m_Strip.tabContainer.components[this.m_Strip.selectedIndex].Find("Container");
            PublicTransportDetailPanel.Quicksort(uIComponent.components, new Comparison<UIComponent>(ComparePassengers));
            this.m_LastSortCriterion = LineSortCriterion.PASSENGER;
            uIComponent.Invalidate();
        }

        public void SetActiveTab(int idx)
        {
            this.m_Strip.selectedIndex = idx;
        }

        public void RefreshLines()
        {
            if (Singleton<TransportManager>.exists)
            {
                int busCount = 0;
                int tramCount = 0;
                int metroCount = 0;
                int trainCount = 0;

                //TLM
                int shipCount = 0;
                int lowBusCount = 0;
                int highBusCount = 0;
                int surfaceMetroCount = 0;
                int bulletTrainCount = 0;

                for (ushort lineIdIterator = 1; lineIdIterator < 256; lineIdIterator += 1)
                {
                    if ((Singleton<TransportManager>.instance.m_lines.m_buffer[(int)lineIdIterator].m_flags & (TransportLine.Flags.Created | TransportLine.Flags.Temporary)) == TransportLine.Flags.Created)
                    {
                        switch (TLMCW.getConfigIndexForLine(lineIdIterator))
                        {
                            case TLMConfigWarehouse.ConfigIndex.BUS_CONFIG:
                                busCount = AddToList(busCount, lineIdIterator, ref m_BusLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.TRAM_CONFIG:
                                tramCount = AddToList(tramCount, lineIdIterator, ref m_TramLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.METRO_CONFIG:
                                metroCount = AddToList(metroCount, lineIdIterator, ref m_MetroLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.TRAIN_CONFIG:
                                trainCount = AddToList(trainCount, lineIdIterator, ref m_TrainLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.BULLET_TRAIN_CONFIG:
                                bulletTrainCount = AddToList(bulletTrainCount, lineIdIterator, ref m_BulletTrainLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.SURFACE_METRO_CONFIG:
                                surfaceMetroCount = AddToList(surfaceMetroCount, lineIdIterator, ref m_SurfaceMetroLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.LOW_BUS_CONFIG:
                                lowBusCount = AddToList(lowBusCount, lineIdIterator, ref m_LowBusLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.HIGH_BUS_CONFIG:
                                highBusCount = AddToList(highBusCount, lineIdIterator, ref m_HighBusLinesContainer);
                                break;
                            case TLMCW.ConfigIndex.SHIP_CONFIG:
                                shipCount = AddToList(shipCount, lineIdIterator, ref m_ShipLinesContainer);
                                break;
                        }
                    }
                }
                RemoveExtraLines(busCount, ref this.m_BusLinesContainer);
                RemoveExtraLines(tramCount, ref this.m_TramLinesContainer);
                RemoveExtraLines(metroCount, ref this.m_MetroLinesContainer);
                RemoveExtraLines(trainCount, ref this.m_TrainLinesContainer);
                RemoveExtraLines(lowBusCount, ref this.m_LowBusLinesContainer);
                RemoveExtraLines(highBusCount, ref this.m_HighBusLinesContainer);
                RemoveExtraLines(bulletTrainCount, ref this.m_BulletTrainLinesContainer);
                RemoveExtraLines(surfaceMetroCount, ref this.m_SurfaceMetroLinesContainer);
                RemoveExtraLines(shipCount, ref this.m_ShipLinesContainer);

                this.m_LinesUpdated = true;
            }
        }

        private static void RemoveExtraLines(int linesCount, ref UIComponent component)
        {
            while (component.components.Count > linesCount)
            {
                UIComponent uIComponent = component.components[linesCount];
                component.RemoveUIComponent(uIComponent);
                UnityEngine.Object.Destroy(uIComponent.gameObject);
            }
        }

        private int AddToList(int count, ushort lineIdIterator, ref UIComponent component)
        {
            TLMPublicTransportLineInfo publicTransportLineInfo;
            TLMUtils.doLog("PreIF");
            TLMUtils.doLog("Count = {0}; Component = {1}; components count = {2}", count, component.ToString(), component.components.Count);
            if (count >= component.components.Count)
            {
                TLMUtils.doLog("IF TRUE");
                var temp = UITemplateManager.Get<PublicTransportLineInfo>(kLineTemplate).gameObject;
                GameObject.Destroy(temp.GetComponent<PublicTransportLineInfo>());
                publicTransportLineInfo = temp.AddComponent<TLMPublicTransportLineInfo>();
                component.AttachUIComponent(publicTransportLineInfo.gameObject);
            }
            else
            {
                TLMUtils.doLog("IF FALSE");
                TLMUtils.doLog("component.components[count] = {0};", component.components[count]);
                publicTransportLineInfo = component.components[count].GetComponent<TLMPublicTransportLineInfo>();
            }
            publicTransportLineInfo.lineID = lineIdIterator;
            publicTransportLineInfo.RefreshData(true, false);
            count++;
            return count;
        }
        bool isChangingTab;
        private void OnTabChanged(UIComponent c, int idx)
        {
            if (this.m_ToggleAll != null)
            {
                isChangingTab = true;
                this.m_ToggleAll.isChecked = this.m_ToggleAllState[idx];
                isChangingTab = false;
            }
        }

        private void CheckChangedFunction(UIComponent c, bool r)
        {
            if (!isChangingTab)
            {
                this.OnChangeVisibleAll(r);
            }
        }

        private void OnChangeVisibleAll(bool visible)
        {
            if (this.m_Strip.selectedIndex > -1 && this.m_Strip.selectedIndex < this.m_Strip.tabContainer.components.Count)
            {
                this.m_ToggleAllState[this.m_Strip.selectedIndex] = visible;
                UIComponent uIComponent = this.m_Strip.tabContainer.components[this.m_Strip.selectedIndex].Find("Container");
                if (uIComponent != null)
                {
                    for (int i = 0; i < uIComponent.components.Count; i++)
                    {
                        UIComponent uIComponent2 = uIComponent.components[i];
                        if (uIComponent2 != null)
                        {
                            UICheckBox uICheckBox = uIComponent2.Find<UICheckBox>("LineVisible");
                            uICheckBox.isChecked = visible;
                        }
                    }
                }
                this.RefreshLines();
            }
        }


        private void Update()
        {
            if (Singleton<TransportManager>.exists && this.m_Ready && this.m_LastLineCount != Singleton<TransportManager>.instance.m_lineCount)
            {
                this.RefreshLines();
                this.m_LastLineCount = Singleton<TransportManager>.instance.m_lineCount;
            }
            if (this.m_LinesUpdated)
            {
                this.m_LinesUpdated = false;
                if (this.m_LastSortCriterion != LineSortCriterion.DEFAULT)
                {
                    if (this.m_LastSortCriterion == LineSortCriterion.NAME)
                    {
                        this.OnNameSort();
                    }
                    else if (this.m_LastSortCriterion == LineSortCriterion.PASSENGER)
                    {
                        this.OnPassengerSort();
                    }
                    else if (this.m_LastSortCriterion == LineSortCriterion.STOP)
                    {
                        this.OnStopSort();
                    }
                    else if (this.m_LastSortCriterion == LineSortCriterion.VEHICLE)
                    {
                        this.OnVehicleSort();
                    }
                }
            }
        }

    }


}