using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The wire form of a RecordedRoute. The runtime types stay readonly on purpose --
    /// sentries hold onto route lists across rounds -- and JsonUtility cannot write readonly
    /// fields, so the route crosses the network as a plain mutable copy and is rebuilt on
    /// the far side. Nothing here is a scene reference: a prop is its HierarchyPath id.
    /// </summary>
    [Serializable]
    public class RouteData
    {
        public int owner;
        public float distance;
        public List<CrumbData> crumbs = new List<CrumbData>();
        public List<DwellData> dwells = new List<DwellData>();

        public static RouteData From(RecordedRoute route)
        {
            var data = new RouteData { owner = route.Owner, distance = route.Distance };
            foreach (var crumb in route.Crumbs)
            {
                data.crumbs.Add(new CrumbData { p = crumb.Position, d = crumb.Direction });
            }

            foreach (var dwell in route.Dwells)
            {
                data.dwells.Add(new DwellData { p = dwell.Position, yaw = dwell.FacingYaw, crumb = dwell.CrumbIndex, prop = dwell.PropId });
            }

            return data;
        }

        public RecordedRoute ToRoute()
        {
            var route = new RecordedRoute { Owner = owner, Distance = distance };
            foreach (var crumb in crumbs) route.Crumbs.Add(new Breadcrumb(crumb.p, crumb.d));
            foreach (var dwell in dwells) route.Dwells.Add(new DwellPoint(dwell.p, dwell.yaw, dwell.crumb, dwell.prop));
            return route;
        }
    }

    [Serializable]
    public class CrumbData
    {
        public Vector3 p;
        public Vector3 d;
    }

    [Serializable]
    public class DwellData
    {
        public Vector3 p;
        public float yaw;
        public int crumb;
        public string prop;
    }
}
