using UnityEngine;
namespace Integration
{
    public static class CampaignTypography
    {
        static Font readable;
        public static Font Get(Font fallback)
        {
            if(readable!=null)return readable;
            foreach(string installed in Font.GetOSInstalledFontNames())
                if(installed=="Malgun Gothic")return readable=Font.CreateDynamicFontFromOSFont(installed,24);
            return fallback;
        }
    }
}
