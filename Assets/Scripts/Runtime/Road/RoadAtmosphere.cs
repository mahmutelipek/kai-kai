using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    public static class RoadAtmosphere
    {
        public static void Apply()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.color = new Color(1f,.97f,.89f); light.intensity = 1.55f;
                    light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(26,145,0);
                }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.53f,.77f,1f);
            RenderSettings.ambientEquatorColor = new Color(.54f,.62f,.54f);
            RenderSettings.ambientGroundColor = new Color(.25f,.3f,.27f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.56f,.78f,.98f); RenderSettings.fogStartDistance = 135f; RenderSettings.fogEndDistance = 360f;
            var sky = Resources.Load<Material>("Art/Feedback/DaySky");
            if (sky != null) RenderSettings.skybox = sky;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            { camera.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.56f,.78f,.98f); }
        }
        public static void Dress(RoadPath path, Transform parent, float start, float end, int chunkIndex)
        {
            Color grass = chunkIndex / 8 % 2 == 0 ? new Color(.20f,.44f,.10f) : new Color(.28f,.48f,.11f);
            for (float d = start + 8; d < end; d += 12)
                for (int side = -1; side <= 1; side += 2)
                {
                    path.Sample(d, out Vector3 p, out float yaw); Vector3 right = SimConvert.YawRight(yaw);
                    float variation = Mathf.Sin(d * .27f + side * 3);
                    var root = new GameObject("Verge decoration"); root.transform.SetParent(parent, false);
                    root.transform.SetPositionAndRotation(p, Quaternion.Euler(0,yaw*Mathf.Rad2Deg,0));
                    // Alternating reflective posts make the road edge clear without blocking the player.
                    PrimitiveFactory.Visual(PrimitiveType.Cube, root.transform, new Vector3(side*6.8f,.45f,0), new Vector3(.16f,.9f,.18f), new Color(.83f,.84f,.72f), "Edge Post");
                    PrimitiveFactory.Visual(PrimitiveType.Cube, root.transform, new Vector3(side*6.8f,.7f,-.11f), new Vector3(.2f,.18f,.03f), new Color(1f,.6f,.14f), "Reflector");
                    PrimitiveFactory.Visual(PrimitiveType.Sphere, root.transform, new Vector3(side*(12+variation*2),.3f,3), new Vector3(2.2f,1.5f,2f), grass, "Shrub");
                    if (Mathf.FloorToInt(d / 12) % 2 == 0)
                        PrimitiveFactory.Visual(PrimitiveType.Sphere, root.transform, new Vector3(side*18,.45f,-2), new Vector3(2.6f,1.6f,2.2f), new Color(.49f,.52f,.46f), "Rock");
                }
            RoadVegetation.Build(path,parent,start,end,chunkIndex);
            // Yellow turn boards and short striped roadside barriers frame the action without narrowing safe lanes.
            for(int side=-1;side<=1;side+=2)
            {
                path.Sample(start+62,out Vector3 p,out float yaw);
                var sign=new GameObject("Turn marker");sign.transform.SetParent(parent,false);
                sign.transform.SetPositionAndRotation(p+SimConvert.YawRight(yaw)*side*7.2f,Quaternion.Euler(0,yaw*Mathf.Rad2Deg,0));
                PrimitiveFactory.Visual(PrimitiveType.Cube,sign.transform,new Vector3(0,.7f,0),new Vector3(.13f,1.4f,.13f),MaterialLibrary.Metal,"Sign post");
                PrimitiveFactory.Visual(PrimitiveType.Cube,sign.transform,new Vector3(0,1.55f,0),new Vector3(1.3f,1.1f,.13f),MaterialLibrary.LineYellow,"Yellow turn sign");
                for(int half=-1;half<=1;half+=2)
                {
                    var mark=PrimitiveFactory.Visual(PrimitiveType.Cube,sign.transform,new Vector3(.03f,1.55f+half*.2f,-.08f),new Vector3(.57f,.14f,.02f),new Color(.08f,.09f,.1f),"Chevron");
                    mark.transform.localRotation=Quaternion.Euler(0,0,half*side*45);
                }
                if(chunkIndex%2==0)
                    for(int i=0;i<6;i++)
                    {
                        path.Sample(start+16+i*2,out Vector3 edge,out float edgeYaw);
                        var barrier=PrimitiveFactory.Visual(PrimitiveType.Cube,parent,Vector3.zero,new Vector3(.28f,.55f,1.8f),i%2==0?new Color(.95f,.3f,.06f):new Color(.9f,.91f,.85f),"Roadside barrier");
                        barrier.transform.SetPositionAndRotation(edge+SimConvert.YawRight(edgeYaw)*side*6.4f+Vector3.up*.27f,Quaternion.Euler(0,edgeYaw*Mathf.Rad2Deg,0));
                    }
            }
            if (chunkIndex % 2 == 0)
            {
                var house = Resources.Load<GameObject>("Art/Environment/CoastalHouse" + (chunkIndex / 2 % 3));
                if (house != null)
                {
                    path.Sample(start + 48, out Vector3 p, out float yaw);
                    var building = Object.Instantiate(house, parent);
                    building.transform.SetPositionAndRotation(p - SimConvert.YawRight(yaw) * 23f, Quaternion.Euler(0,yaw*Mathf.Rad2Deg+90,0));
                }
            }
            // Broad stylized hills close the horizon. They have no colliders and retire with their road chunk.
            for (int side = -1; side <= 1; side += 2)
            {
                path.Sample(start + 40, out Vector3 p, out float yaw);
                var hill = PrimitiveFactory.Visual(PrimitiveType.Sphere, parent, Vector3.zero, new Vector3(62,28,96), grass, "Rolling Hill");
                hill.transform.position = p + SimConvert.YawRight(yaw) * side * 53 + Vector3.down * 8;
                hill.transform.rotation = Quaternion.Euler(0,yaw*Mathf.Rad2Deg,0);
            }
        }
    }
}
