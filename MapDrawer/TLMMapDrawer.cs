using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
/**
   OBS: Mudar a forma de pintar as linhas, criar caminhos comuns entre estações iguais (ajuda no tram)
   ver como fazer linhas de sentido único no tram
*/
namespace Klyte.TransportLinesManager.MapDrawer
{
    public class TLMMapDrawer
    {

        private static Color almostWhite = new Color(0.9f, 0.9f, 0.9f);

        public static void drawCityMap()
        {

            TLMController controller = TLMController.instance;
            Dictionary<TransportInfo.TransportType, List<ushort>> linesByType = new Dictionary<TransportInfo.TransportType, List<ushort>>();
            linesByType[TransportInfo.TransportType.Metro] = new List<ushort>();
            linesByType[TransportInfo.TransportType.Train] = new List<ushort>();
            linesByType[TransportInfo.TransportType.Tram] = new List<ushort>();
            linesByType[TransportInfo.TransportType.Ship] = new List<ushort>();

            //			List<int> usedX = new List<int> ();
            //			List<int> usedY = new List<int> ();
            int nextStationId = 1;
            for (ushort lineId = 0; lineId < controller.tm.m_lines.m_size; lineId++)
            {
                TransportLine t = controller.tm.m_lines.m_buffer[(int)lineId];
                if (t.m_lineNumber > 0 && (t.Info.m_transportType == TransportInfo.TransportType.Metro 
                    || t.Info.m_transportType == TransportInfo.TransportType.Train
                    || t.Info.m_transportType == TransportInfo.TransportType.Tram 
                    || t.Info.m_transportType == TransportInfo.TransportType.Ship))
                {
                    switch (t.Info.m_transportType)
                    {
                        case TransportInfo.TransportType.Ship:
                        case TransportInfo.TransportType.Train:
                        case TransportInfo.TransportType.Metro:
                        case TransportInfo.TransportType.Tram:
                            linesByType[t.Info.m_transportType].Add(lineId);
                            break;
                    }
                }

            }

            CalculateCoords calc = TLMLineUtils.gridPosition81Tiles;
            NetManager nm = NetManager.instance;
            float invPrecision = 32;
            //Restart:
            Dictionary<int, List<int>> positions = new Dictionary<int, List<int>>();
            List<Station> stations = new List<Station>();
            Dictionary<Segment2, Color32> svgLines = new Dictionary<Segment2, Color32>();
            Dictionary<ushort, MapTransportLine> transportLines = new Dictionary<ushort, MapTransportLine>();
            foreach (TransportInfo.TransportType tt in new TransportInfo.TransportType[] { TransportInfo.TransportType.Ship, TransportInfo.TransportType.Train, TransportInfo.TransportType.Metro, TransportInfo.TransportType.Tram })
            {
                foreach (ushort lineId in linesByType[tt])
                {
                    TransportLine t = controller.tm.m_lines.m_buffer[(int)lineId];
                    float range = 75f;
                    switch (tt)
                    {
                        case TransportInfo.TransportType.Ship:
                            range = 150f;
                            break;
                        case TransportInfo.TransportType.Metro:
                        case TransportInfo.TransportType.Train:
                            range = 100f;
                            break;
                    }


                    int stopsCount = t.CountStops(lineId);
                    if (stopsCount == 0)
                    {
                        continue;
                    }
                    Color color = t.m_color;
                    Vector2 ultPos = Vector2.zero;
                    bool day, night;
                    t.GetActive(out day, out night);
                    transportLines[lineId] = new MapTransportLine(color, day, night, lineId);
                    int startStop = 0;
                    int finalStop = stopsCount;

                    for (int j = startStop; j < finalStop; j++)
                    {
                        //						Debug.Log ("ULT POS:" + ultPos);
                        ushort nextStop = t.GetStop(j % stopsCount);
                        ItemClass.Service service;
                        ItemClass.SubService nil2;
                        string prefix;
                        ushort buildingId;
                        string name = TLMUtils.getStationName(nextStop, lineId, t.Info.m_stationSubService, out service, out nil2, out prefix, out buildingId);

                        Vector3 worldPos = TLMUtils.getStationBuildingPosition(nextStop, t.Info.m_stationSubService);
                        Vector2 pos2D = calc(worldPos, invPrecision);
                        Vector2 gridAdd = Vector2.zero;


                        var idx = stations.FirstOrDefault(x => x.stops.Contains(nextStop) || x.centralPos == pos2D);
                        if (idx != null)
                        {
                            transportLines[lineId].addStation(ref idx);
                        }
                        else
                        {
                            //if (positions.containskey((int)pos2d.x) && positions[(int)pos2d.x].contains((int)pos2d.y))
                            //{
                            //    float exp = (float)(math.log(invprecision) / math.log(2)) - 1;
                            //    invprecision = (float)math.pow(2, exp);
                            //    goto restart;
                            //}
                            List<ushort> nearStops = new List<ushort>();
                            TLMLineUtils.GetNearStopPoints(worldPos, range, ref nearStops, new ItemClass.SubService[] { ItemClass.SubService.PublicTransportShip }, 10);
                            TLMLineUtils.GetNearStopPoints(worldPos, range, ref nearStops, new ItemClass.SubService[] { ItemClass.SubService.PublicTransportTrain, ItemClass.SubService.PublicTransportMetro }, 10);
                            TLMLineUtils.GetNearStopPoints(worldPos, range, ref nearStops, new ItemClass.SubService[] { ItemClass.SubService.PublicTransportTram }, 10);
                            TLMUtils.doLog("Station: ${0}; nearStops: ${1}", name, string.Join(",", nearStops.Select(x => x.ToString()).ToArray()));
                            Station thisStation = new Station(name, pos2D, nearStops, nextStationId++, service, nextStop);
                            stations.Add(thisStation);
                            transportLines[lineId].addStation(ref thisStation);
                        }
                        if (!positions.ContainsKey((int)pos2D.x))
                        {
                            positions[(int)pos2D.x] = new List<int>();
                        }
                        positions[(int)pos2D.x].Add((int)pos2D.y);
                        //						Debug.Log ("POS:" + pos);
                        ultPos = pos2D;
                    }
                }
            }
            printToSVG(stations, transportLines, Singleton<SimulationManager>.instance.m_metaData.m_CityName + "_" + Singleton<SimulationManager>.instance.m_currentGameTime.ToString("yyyy.MM.dd"));
        }

        public static string printToSVG(List<Station> stations, Dictionary<ushort, MapTransportLine> transportLines, string mapName)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach (var s in stations)
            {
                if (s.centralPos.x > maxX)
                {
                    maxX = s.centralPos.x;
                }
                if (s.centralPos.y > maxY)
                {
                    maxY = s.centralPos.y;
                }
                if (s.centralPos.x < minX)
                {
                    minX = s.centralPos.x;
                }
                if (s.centralPos.y < minY)
                {
                    minY = s.centralPos.y;
                }
            }
            return drawSVG(stations, transportLines, mapName, minX, minY, maxX, maxY);
        }

        private static string drawSVG(List<Station> stations, Dictionary<ushort, MapTransportLine> transportLines, string mapName, float minX, float minY, float maxX, float maxY)
        {
            float maxRadius = Math.Max(stations.Max(x => x.getAllStationOffsetPoints().Count) * 2 + 2, 10);

            SVGTemplate svg = new SVGTemplate((int)((maxY - minY + 16)), (int)((maxX - minX + 16)), maxRadius, minX - maxRadius, minY - maxRadius);

            var linesOrdened = transportLines.OrderBy(x => getLineUID(x.Key)).ToList();
            //ordena pela quantidade de linhas passando
            stations = stations.OrderBy(x => x.linesPassingCount).ToList();

            //calcula as posições de todas as estações no mapa
            foreach (var line in linesOrdened)
            {
                var station0 = line.Value[0];
                var prevPos = station0.getPositionForLine(line.Key, line.Value[1].centralPos);
                for (int i = 1; i < line.Value.stationsCount(); i++)
                {
                    prevPos = line.Value[i].getPositionForLine(line.Key, prevPos);
                }
            }
            //adiciona as exceções
            svg.addStationsToExceptionMap(stations);
            //pinta as linhas
            foreach (var line in linesOrdened)
            {
                svg.addTransportLine(line.Value, line.Key);
            }
            svg.drawAllLines();
            foreach (var station in stations)
            {
                svg.addStation(station, transportLines);
            }
            String folder = "Transport Lines Manager";
            if (File.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.Directory) != FileAttributes.Directory)
            {
                File.Delete(folder);
            }
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            String filename = folder + Path.DirectorySeparatorChar + "TLM_MAP_" + mapName + ".html";
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            var sr = File.CreateText(filename);
            sr.WriteLine(svg.getResult());
            sr.Close();
            return filename;
        }

        private static int getLineUID(ushort lineId)
        {
            return lineId;
        }


        private delegate Vector2 CalculateCoords(Vector3 pos, float invPrecision);
    }

    public class Station
    {
        public string name
        {
            get; set;
        }
        private Vector2 originalCentralPos
        {
            get; set;
        }
        public Vector2 centralPos
        {
            get; set;
        }
        public Vector2 finalPos
        {
            get; set;
        }
        public int linesPassingCount
        {
            get
            {
                return linesPassing.Count;
            }
        }
        public List<ushort> stops
        {
            get; set;
        }
        public ushort stopId
        {
            get; internal set;
        }
        public Vector2 writePoint
        {
            get
            {
                return centralPos + lastPoint;
            }
        }
        public float writeAngle
        {
            get
            {
                CardinalPoint direction = CardinalPoint.E;
                for (int i = 0; i < 8; i++)
                {
                    if (stationConnections.ContainsKey(direction))
                    {
                        direction++;
                    }
                    else
                    {
                        break;
                    }
                }
                return direction.getCardinalAngle();
            }
        }

        private List<ushort> linesPassing = new List<ushort>();
        private Vector2 lastPoint = Vector2.zero;
        private int id;
        private List<int> optimizedWithStationsId = new List<int>();
        private ItemClass.Service service;

        public Station(string n, Vector2 pos, List<ushort> stops, int stationId, ItemClass.Service service, ushort stopId, ushort lineId) : this(n, pos, stops, stationId, service, stopId)
        {
            addLine(lineId);
        }
        public Station(string n, Vector2 pos, List<ushort> stops, int stationId, ItemClass.Service service, ushort stopId)
        {
            name = n;
            originalCentralPos = pos;
            centralPos = pos;
            this.stops = stops;
            id = stationId;
            this.stopId = stopId;
            this.service = service;
        }

        public void addLine(ushort lineId)
        {
            if (!linesPassing.Contains(lineId))
            {
                linesPassing.Add(lineId);
            }
        }

        public int getLineIdx(ushort lineId)
        {
            return linesPassing.IndexOf(lineId);
        }

        public CardinalPoint getDirectionForStation(Station s)
        {
            return stationConnections.FirstOrDefault(x => x.Value.stopId == s.stopId).Key;
        }


        public Vector2 getPositionForLine(ushort lineIndex, Vector2 to)
        {

            return originalCentralPos;

        }

        private Dictionary<CardinalPoint, Station> stationConnections = new Dictionary<CardinalPoint, Station>();

        public CardinalPoint reserveExit(Station s2)
        {
            if (stationConnections.Count >= 8)
            {
                return CardinalPoint.ZERO;
            }
            if (stops.Contains(s2.stopId) || s2.stops.Contains(stopId))
            {
                return CardinalPoint.ZERO;
            }
            var s = stationConnections.FirstOrDefault(x => x.Value.stops.Contains(s2.stopId));
            if (!s.Equals(default(KeyValuePair<CardinalPoint, Station>)))
            {
                return s.Key;
            }

            CardinalPoint direction = CardinalPoint.getCardinal2D(centralPos, s2.centralPos);
            CardinalPoint directionOr = direction;

            CardinalPoint directionAlt = CardinalPoint.getCardinal2D4(centralPos, s2.centralPos);

            bool isForward = direction > directionAlt;

            if (stationConnections.ContainsKey(direction))
            {
                if (isForward)
                {
                    direction++;
                }
                else
                {
                    direction--;
                }
            }

            if (stationConnections.ContainsKey(direction))
            {
                direction = directionOr;
                if (isForward)
                {
                    direction--;
                }
                else
                {
                    direction++;
                }
            }

            stationConnections[direction] = s2;
            return direction;
        }

        //public string getIntegrationLinePath(Vector2 offset, float multiplier)
        //{
        //    if (linesPos.Count <= 1) return string.Empty;
        //    StringBuilder result = new StringBuilder();
        //    Vector2 from = originalCentralPos - offset;
        //    foreach (Vector2 point in linesPos.Keys)
        //    {
        //        if (point != Vector2.zero)
        //        {
        //            Vector2 to = originalCentralPos + point - offset;
        //            result.Append(string.Format(" M {0},{1} L {2},{3} ", from.x * multiplier, from.y * multiplier, to.x * multiplier, to.y * multiplier));
        //        }
        //    }
        //    return result.ToString();
        //}


        public List<ushort> getAllStationOffsetPoints()
        {
            return linesPassing;
        }

    }

    public struct CardinalPoint
    {
        public static CardinalPoint getCardinalPoint(float angle, float diagSize = 45)
        {
            angle %= 360;
            angle += 360;
            angle %= 360;

            if (angle < 135 + diagSize / 2 && angle >= 135 - diagSize / 2)
            {
                return CardinalPoint.NW;
            }
            else if (angle < 135 - diagSize / 2 && angle >= 45 + diagSize / 2)
            {
                return CardinalPoint.N;
            }
            else if (angle < 45 + diagSize / 2 && angle >= 45 - diagSize / 2)
            {
                return CardinalPoint.NE;
            }
            else if (angle < 45 - diagSize / 2 || angle >= 315 + diagSize / 2)
            {
                return CardinalPoint.E;
            }
            else if (angle < 315 + diagSize / 2 && angle >= 315 - diagSize / 2)
            {
                return CardinalPoint.SE;
            }
            else if (angle < 315 - diagSize / 2 && angle >= 225 + diagSize / 2)
            {
                return CardinalPoint.S;
            }
            else if (angle < 225 + diagSize / 2 && angle >= 225 - diagSize / 2)
            {
                return CardinalPoint.SW;
            }
            else {
                return CardinalPoint.W;
            }

        }

        public static CardinalPoint getCardinalPoint4(float angle)
        {
            angle %= 360;
            angle += 360;
            angle %= 360;

            if (angle < 135f && angle >= 45f)
            {
                return CardinalPoint.N;
            }
            else if (angle < 45f || angle >= 315f)
            {
                return CardinalPoint.E;
            }
            else if (angle < 315f && angle >= 225f)
            {
                return CardinalPoint.S;
            }
            else {
                return CardinalPoint.W;
            }
        }

        private CardinalInternal InternalValue { get; set; }

        public CardinalInternal Value { get { return InternalValue; } }

        public static readonly CardinalPoint N = CardinalInternal.N;
        public static readonly CardinalPoint E = CardinalInternal.E;
        public static readonly CardinalPoint S = CardinalInternal.S;
        public static readonly CardinalPoint W = CardinalInternal.W;
        public static readonly CardinalPoint NE = CardinalInternal.NE;
        public static readonly CardinalPoint SE = CardinalInternal.SE;
        public static readonly CardinalPoint SW = CardinalInternal.SW;
        public static readonly CardinalPoint NW = CardinalInternal.NW;
        public static readonly CardinalPoint ZERO = CardinalInternal.ZERO;

        public static implicit operator CardinalPoint(CardinalInternal otherType)
        {
            return new CardinalPoint
            {
                InternalValue = otherType
            };
        }

        public static implicit operator CardinalInternal(CardinalPoint otherType)
        {
            return otherType.InternalValue;
        }

        public int stepsTo(CardinalPoint other)
        {
            if (other.InternalValue == InternalValue) return 0;
            if ((((int)other.InternalValue) & ((int)other.InternalValue - 1)) != 0 || (((int)InternalValue) & ((int)InternalValue - 1)) != 0) return int.MaxValue;
            CardinalPoint temp = other;
            int count = 0;
            while (temp.InternalValue != this.InternalValue)
            {
                temp++;
                count++;
            }
            if (count > 4) count = count - 8;
            return count;
        }

        public static int operator -(CardinalPoint c, CardinalPoint other)
        {
            return c.stepsTo(other);
        }

        public Vector2 getCardinalOffset()
        {
            switch (InternalValue)
            {
                case CardinalPoint.CardinalInternal.E:
                    return new Vector2(1, 0);
                case CardinalPoint.CardinalInternal.W:
                    return new Vector2(-1, 0);
                case CardinalPoint.CardinalInternal.N:
                    return new Vector2(0, 1);
                case CardinalPoint.CardinalInternal.S:
                    return new Vector2(0, -1);
                case CardinalPoint.CardinalInternal.NE:
                    return new Vector2(1, 1);
                case CardinalPoint.CardinalInternal.NW:
                    return new Vector2(-1, 1);
                case CardinalPoint.CardinalInternal.SE:
                    return new Vector2(1, -1);
                case CardinalPoint.CardinalInternal.SW:
                    return new Vector2(-1, -1);
            }
            return Vector2.zero;
        }


        public Vector2 getCardinalOffset2D()
        {
            switch (InternalValue)
            {
                case CardinalPoint.CardinalInternal.E:
                    return new Vector2(1, 0);
                case CardinalPoint.CardinalInternal.W:
                    return new Vector2(-1, 0);
                case CardinalPoint.CardinalInternal.S:
                    return new Vector2(0, 1);
                case CardinalPoint.CardinalInternal.N:
                    return new Vector2(0, -1);
                case CardinalPoint.CardinalInternal.SE:
                    return new Vector2(1, 1);
                case CardinalPoint.CardinalInternal.SW:
                    return new Vector2(-1, 1);
                case CardinalPoint.CardinalInternal.NE:
                    return new Vector2(1, -1);
                case CardinalPoint.CardinalInternal.NW:
                    return new Vector2(-1, -1);
            }
            return Vector2.zero;
        }

        public int getCardinalAngle()
        {
            switch (InternalValue)
            {
                case CardinalPoint.CardinalInternal.E:
                    return 0;
                case CardinalPoint.CardinalInternal.W:
                    return 180;
                case CardinalPoint.CardinalInternal.N:
                    return 90;
                case CardinalPoint.CardinalInternal.S:
                    return 270;
                case CardinalPoint.CardinalInternal.NE:
                    return 45;
                case CardinalPoint.CardinalInternal.NW:
                    return 135;
                case CardinalPoint.CardinalInternal.SE:
                    return 315;
                case CardinalPoint.CardinalInternal.SW:
                    return 225;
            }
            return 0;
        }

        public static CardinalPoint operator ++(CardinalPoint c)
        {
            switch (c.InternalValue)
            {
                case CardinalInternal.N:
                    return NE;
                case CardinalInternal.NE:
                    return E;
                case CardinalInternal.E:
                    return SE;
                case CardinalInternal.SE:
                    return S;
                case CardinalInternal.S:
                    return SW;
                case CardinalInternal.SW:
                    return W;
                case CardinalInternal.W:
                    return NW;
                case CardinalInternal.NW:
                    return N;
                default:
                    return ZERO;
            }
        }

        public static CardinalPoint operator --(CardinalPoint c)
        {
            switch (c.InternalValue)
            {
                case CardinalInternal.N:
                    return NW;
                case CardinalInternal.NE:
                    return N;
                case CardinalInternal.E:
                    return NE;
                case CardinalInternal.SE:
                    return E;
                case CardinalInternal.S:
                    return SE;
                case CardinalInternal.SW:
                    return S;
                case CardinalInternal.W:
                    return SW;
                case CardinalInternal.NW:
                    return W;
                default:
                    return ZERO;
            }
        }

        public static CardinalPoint operator &(CardinalPoint c1, CardinalPoint c2)
        {
            return new CardinalPoint
            {
                InternalValue = c1.InternalValue & c2.InternalValue
            };
        }

        public static CardinalPoint operator |(CardinalPoint c1, CardinalPoint c2)
        {
            return new CardinalPoint
            {
                InternalValue = c1.InternalValue | c2.InternalValue
            };
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object o)
        {

            return o.GetType() == GetType() && this == ((CardinalPoint)o);
        }

        public static bool operator ==(CardinalPoint c1, CardinalPoint c2)
        {
            return c1.InternalValue == c2.InternalValue;
        }

        public static bool operator <(CardinalPoint left, CardinalPoint right)
        {
            return (Compare(left, right) < 0);
        }

        public static bool operator >(CardinalPoint left, CardinalPoint right)
        {
            return (Compare(left, right) > 0);
        }

        public int CompareTo(CardinalPoint other)
        {
            if (this == other) return 0;
            var a = getCardinalAngle();
            var b = other.getCardinalAngle() + 360;
            if (b - a > 180)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }

        public static int Compare(CardinalPoint left, CardinalPoint right)
        {
            if (object.ReferenceEquals(left, right))
            {
                return 0;
            }
            if (object.ReferenceEquals(left, null))
            {
                return -1;
            }
            return left.CompareTo(right);
        }


        public static bool operator !=(CardinalPoint c1, CardinalPoint c2)
        {
            return c1.InternalValue != c2.InternalValue;
        }

        public static CardinalPoint operator ~(CardinalPoint c)
        {
            switch (c.InternalValue)
            {
                case CardinalInternal.N:
                    return S;
                case CardinalInternal.NE:
                    return SW;
                case CardinalInternal.E:
                    return W;
                case CardinalInternal.SE:
                    return NW;
                case CardinalInternal.S:
                    return N;
                case CardinalInternal.SW:
                    return NE;
                case CardinalInternal.W:
                    return E;
                case CardinalInternal.NW:
                    return SE;
                default:
                    return ZERO;
            };
        }

        public enum CardinalInternal
        {
            N = 1,
            NE = 2,
            E = 4,
            SE = 8,
            S = 0x10,
            SW = 0x20,
            W = 0x40,
            NW = 0x80,
            ZERO = 0
        }

        public Vector2 getPointForAngle(Vector2 p1, float distance)
        {
            return p1 + this.getCardinalOffset() * distance;
        }


        public override string ToString()
        {
            return InternalValue.ToString();
        }

        public static CardinalPoint getCardinal2D(Vector2 p1, Vector2 p2)
        {

            if (Math.Abs(p1.x - p2.x) < 3)
            {
                return p1.y > p2.y ? CardinalPoint.N : CardinalPoint.S;
            }
            if (Math.Abs(p1.y - p2.y) < 3)
            {
                return p1.x > p2.x ? CardinalPoint.W : CardinalPoint.E;
            }

            var Δ = p1 - p2;
            float α = (float)(Math.Atan(Δ.x / Δ.y) * 180 / Math.PI);
            return getCardinalPoint((Δ.x > 0 ? 180 : 0) - α, 90);
        }

        public static CardinalPoint getCardinal2D4(Vector2 p1, Vector2 p2)
        {
            if (p1.x == p2.x)
            {
                return p1.y > p2.y ? CardinalPoint.N : CardinalPoint.S;
            }

            if (p1.y == p2.y)
            {
                return p1.x > p2.x ? CardinalPoint.W : CardinalPoint.E;
            }

            var Δ = p1 - p2;
            float α = (float)(Math.Atan(Δ.x / Δ.y) * 180 / Math.PI);
            return getCardinalPoint4((Δ.x > 0 ? 180 : 0) - α);
        }

    }


    public class SVGTemplate
    {

        /// <summary>
        /// The header.<>
        /// 0 = Height
        /// 1 = Width
        /// </summary>
        public string getHtmlHeader(int height, int width)
        {
            return "<!DOCTYPE html>" +
             "<html><head> <meta charset=\"UTF-8\"> " +
             "<style>" +
            ResourceLoader.loadResourceString("MapDrawer.lineDrawBasicCss.css") +
             "</style>" +
             "</head><body>" +
             string.Format("<svg height='{0}' width='{1}'>", height, width) +
             "<defs>" +
             "<marker orient=\"auto\" markerHeight=\"6\" markerWidth=\"6\" refY=\"2.5\" refX=\"1\" viewBox=\"0 0 10 5\" id=\"Triangle1\"><path d=\"M 0 0 L 10 2.5 L 0 5 z\"/></marker>" +
             "<marker orient=\"auto\" markerHeight=\"6\" markerWidth=\"6\" refY=\"2.5\" refX=\"1\" viewBox=\"0 0 10 5\" id=\"Triangle2\"><path d=\"M 10 0 L 0 2.5 L 10 5 z\"/></marker>" +
             "</defs>";

        }
        /// <summary>
        /// The line segment. <>
        /// 0 = X1 
        /// 1 = Y1
        /// 2 = X2 
        /// 3 = Y2
        /// 4 = R
        /// 5 = G 
        /// 6 = B 
        /// </summary>
        private string getLineSegmentTemplate()
        {
            return "<line x1='{0}' y1='{1}' x2='{2}' y2='{3}' style='stroke:rgb({4},{5},{6});stroke-width:" + multiplier + "' stroke-linecap='round'/>";
        }
        /// <summary>
        /// The line
        /// 0 = path;
        /// 1 = R;
        /// 2 = G;
        /// 3 = B;
        /// 4 = Line type;
        /// </summary>
       // private readonly string pathLine = "<path d='{0}' class=\"path{4}\" style='stroke:rgb({1},{2},{3});' stroke-linejoin=\"round\" stroke-linecap=\"round\"/>";
        ///// <summary>
        ///// The integration.<>
        ///// 0 = X 
        ///// 1 = Y 
        ///// 2 = Ang (º) 
        ///// 3 = Offset 
        ///// </summary>
        //		private readonly string integration = "<g transform=\" translate({0},{1}) rotate({2}, {0},{1})\">" +
        //			"<line x1=\"0\" y1=\"0\" x2=\"{3}\" y2=\"0\" style=\"stroke:rgb(155,155,155);stroke-width:30\" stroke-linecap=\"round\"/>" +
        //			"<circle cx=\"0\" cy=\"0\" r=\"12\" fill=\"white\" />" +
        //			"<circle cx=\"{3}\" cy=\"0\" r=\"12\" fill=\"white\" />" +
        //			"</g>";
        /// <summary>
        /// The station.<>
        /// 0 = X 
        /// 1 = Y 
        /// 2 = Ang (º) 
        /// 3 = Station Name 
        /// </summary>
        //private static string getStationTemplate()
        //{
        //    return "<g transform=\"rotate({2},{0},{1}) translate({0},{1})\">" +
        //            "<circle cx=\"0\" cy=\"0\" r=\"" + maxRadius * 0.4 + "\" fill=\"white\" style=\"stroke:rgb(155,155,155);stroke-width:1\"/>" +
        //            "<text x=\"" + maxRadius * 0.8 + "\" y=\"" + maxRadius / 6 + "\" fill=\"black\" style=\"stroke:rgb(255,255,255);stroke-width:1\">{3}</text>" +
        //            "</g>";
        //}

        private static string getStationPointTemplate(int idx, Color32 lineColor)
        {
            int baseRadius = 3;
            int additionalRadius = 2;
            return "<circle style=\"stroke:rgb(" + lineColor.r + "," + lineColor.g + "," + lineColor.b + "); stroke-width:2\" fill=\"" + (idx == 0 ? "white" : "transparent") + "\" r=\"" + (baseRadius + additionalRadius * idx) + "\" cy=\"0\" cx=\"0\" transform=\"translate({0},{1})\"/>";
        }
        //   private readonly string stationNameTemplate = "<text transform=\"rotate({2},{0},{1}) translate({0},{1})\" x=\"" + RADIUS * 0.8 + "\" y=\"" + RADIUS / 6 + "\" fill=\"black\" style=\"text-shadow: 0px 0px 6px white;\">{3}</text>";
        private string getStationNameTemplate(int stops)
        {
            float translate = stops + 2;
            return "<div class='stationContainer' style='top: {1}px; left: {0}px;' ><p style='transform: rotate({2}deg) translate(" + translate + "px, " + translate + "px) ;'>{3}</p></div>";
        }
        private string getStationNameInverseTemplate(int stops)
        {
            float translate = stops + 2;
            return "<div class='stationContainer' style='top: {1}px; left: {0}px;' ><p style='transform: rotate({2}deg) translate(" + translate + "px, " + translate + "px) ;'><b>{3}</b></p></div>";
        }


        /// <summary>
        /// The metro line symbol.<>
        /// 0 = X 
        /// 1 = Y 
        /// 2 = Line Name 
        /// 3 = R
        /// 4 = G
        /// 5 = B
        /// </summary>
        //private readonly string metroLineSymbol = "<g transform=\"translate({0},{1})\">" +
        //    "  <rect x=\"" + -maxRadius + "\" y=\"" + -maxRadius + "\" width=\"" + maxRadius * 2 + "\" height=\"" + maxRadius * 2 + "\" fill=\"rgb({3},{4},{5})\" stroke=\"black\" stroke-width=\"1\" />" +
        //    "<text x=\"0\" y=\"" + maxRadius / 3 + "\" fill=\"white\"  stroke=\"black\" stroke-width=\"0.5\" style=\"font-size:" + maxRadius + "px\"   text-anchor=\"middle\">{2}</text>" +
        //    "</g>";


        /// <summary>
        /// The train line symbol.<>
        /// 0 = X 
        /// 1 = Y 
        /// 2 = Line Name 
        /// 3 = R
        /// 4 = G
        /// 5 = B
        /// </summary>
        //private readonly string trainLineSymbol = "<g transform=\"translate({0},{1})\">" +
        //    "<circle cx=\"0\" cy=\"0\"r=\"" + maxRadius + "\" fill=\"rgb({3},{4},{5})\" stroke=\"black\" stroke-width=\"1\" />" +
        //    "<text x=\"0\" y=\"" + maxRadius / 3 + "\" fill=\"white\"  stroke=\"black\" stroke-width=\"0.5\" style=\"font-size:" + maxRadius + "px\"   text-anchor=\"middle\">{2}</text>" +
        //    "</g>";
        /// <summary>
        /// The footer.
        /// </summary>
        public readonly string footer = "</body></html>";



        private LineSegmentStationsManager segmentManager = new LineSegmentStationsManager();
        private StringBuilder svgPart = new StringBuilder();
        private StringBuilder htmlPart = new StringBuilder();
        private float multiplier;
        private int height;
        private int width;




        private Vector2 offset;

        public SVGTemplate(int width, int height, float multiplier = 1, float offsetX = 0, float offsetY = 0)
        {
            this.multiplier = multiplier;
            this.width = (int)(width * multiplier);
            this.height = (int)(height * multiplier);
            this.offset = new Vector2(offsetX, offsetY);
        }

        public string getResult()
        {
            StringBuilder document = new StringBuilder(getHtmlHeader(width, height));
            document.Append(svgPart);
            document.Append("</svg>");
            document.Append(htmlPart);
            document.Append(footer);
            return document.ToString();
        }

        public void addStationsToExceptionMap(List<Station> stations)
        {
            foreach (var s in stations)
            {
                segmentManager.addStationToAllRangeMaps(s.centralPos);
            }
        }

        public void addStation(Station s, Dictionary<ushort, MapTransportLine> lines)
        {
            bool inverse = false;
            string name = s.name;
            var angle = s.writeAngle;
            switch (CardinalPoint.getCardinalPoint(angle).Value)
            {
                case CardinalPoint.CardinalInternal.NW:
                case CardinalPoint.CardinalInternal.W:
                case CardinalPoint.CardinalInternal.SW:
                    inverse = true;
                    break;
            }

            foreach (var pos in s.getAllStationOffsetPoints())
            {
                var point = s.centralPos - offset;// + pos.Key;
                var line = lines[pos];
                svgPart.AppendFormat(getStationPointTemplate(s.getLineIdx(pos), line.lineColor), point.x * multiplier, (point.y * multiplier));
            }
            var namePoint = s.writePoint - offset;
            htmlPart.AppendFormat(inverse ? getStationNameInverseTemplate(s.getAllStationOffsetPoints().Count) : getStationNameTemplate(s.getAllStationOffsetPoints().Count), (namePoint.x + 0.5) * multiplier, ((namePoint.y + 0.5) * multiplier), angle, s.name);

        }

        public void addTransportLine(MapTransportLine points, ushort transportLineIdx)
        {
            var count = points.stationsCount();
            for (int i = 1; i <= count; i++)
            {
                Station s1 = points[i - 1];
                Station s2 = points[i % count];
                segmentManager.addLine(s1, s2, points, LineSegmentStationsManager.Direction.S1_TO_S2);
            }
        }

        internal void drawAllLines()
        {
            TransportLine[] tls = Singleton<TransportManager>.instance.m_lines.m_buffer;
            var segments = segmentManager.getSegments();
            string mainStyle = "<polyline points=\"{0}\" class=\"path{4}\" style='stroke:rgb({1},{2},{3});' stroke-linejoin=\"round\" stroke-linecap=\"round\" marker-mid=\"url(#5)\"/> ";
            foreach (var segment in segments)
            {
                List<Vector2> basePoints = segment.path;
                for (int i = 0; i < basePoints.Count; i++)
                {
                    basePoints[i] = (basePoints[i] - offset) * multiplier;
                }
                float offsetNeg = 0;
                float offsetPos = 0;
                CardinalPoint dir = CardinalPoint.getCardinal2D(segment.s1.centralPos, segment.s2.centralPos);
                dir++;
                dir++;
                Vector2 offsetDir = dir.getCardinalOffset2D();
                foreach (var line in segment.lines)
                {
                    float width = 0;
                    TransportInfo.TransportType tt = tls[line.Key.lineId].Info.m_transportType;
                    switch (tt)
                    {
                        case TransportInfo.TransportType.Tram:
                        case TransportInfo.TransportType.Bus:
                            width = 1;
                            break;
                        case TransportInfo.TransportType.Train:
                            width = 5;
                            break;
                        case TransportInfo.TransportType.Metro:
                            width = 2.5f;
                            break;
                        case TransportInfo.TransportType.Ship:
                            width = 8;
                            break;
                    }
                    float coordMultiplier = 0;
                    if (offsetNeg > offsetPos)
                    {
                        coordMultiplier = (offsetPos + width / 2);
                        offsetPos += width;
                    }
                    else if (offsetNeg < offsetPos)
                    {
                        coordMultiplier = -(offsetNeg + width / 2);
                        offsetNeg += width;
                    }
                    else
                    {
                        offsetPos = width / 2;
                        offsetNeg = width / 2;
                    }

                    var lineTotalOffset = offsetDir * coordMultiplier * 2;
                    Vector2[] points = new Vector2[basePoints.Count];
                    for (int i = 0; i < basePoints.Count; i++)
                    {
                        if (i == 0)
                        {
                            var cp = segment.s1.getDirectionForStation(segment.s2);
                            cp++;
                            cp++;
                            points[i] = basePoints[i] + cp.getCardinalOffset2D() * coordMultiplier * 2;
                        }
                        else if (i == basePoints.Count - 1)
                        {
                            var cp = segment.s2.getDirectionForStation(segment.s1);
                            cp++;
                            cp++;
                            points[i] = basePoints[i] + cp.getCardinalOffset2D() * coordMultiplier * 2;
                        }
                        else
                        {
                            points[i] = basePoints[i] + lineTotalOffset;
                        }
                    }
                    svgPart.AppendFormat(mainStyle, string.Join(" ", points.Select(x => "" + x.x + "," + x.y).ToArray()), line.Key.lineColor.r, line.Key.lineColor.g, line.Key.lineColor.b, tt.ToString(), line.Value);
                }
            }
        }







        //public void addMetroLineIndication(Vector2 point, string name, Color32 color)
        //{
        //    point -= offset;
        //    svgPart.AppendFormat(metroLineSymbol, point.x * multiplier, (point.y * multiplier), name, color.r, color.g, color.b);
        //}

        //public void addTrainLineSegment(Vector2 point, string name, Color32 color)
        //{
        //    point -= offset;
        //    svgPart.AppendFormat(trainLineSymbol, point.x * multiplier, (point.y * multiplier), name, color.r, color.g, color.b);
        //}
    }

}

