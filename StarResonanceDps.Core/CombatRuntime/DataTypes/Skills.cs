// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes.Skills
{
    public class SkillLevelInfo
    {
        public int SkillId = 0;
        public int CurrentLevel = 0;
        public int Tier = 0;
        public string Icon = "";

        public SkillLevelInfo()
        {

        }

        public SkillLevelInfo(Zproto.SkillLevelInfo skillLevelInfo)
        {;
            SkillId = skillLevelInfo.SkillId;
            CurrentLevel = skillLevelInfo.CurrentLevel;
            Tier = skillLevelInfo.RemodelLevel;

            if (HelperMethods.DataTables.Skills.Data.TryGetValue(SkillId.ToString(), out var skill))
            {
                Icon = skill.Icon;
            }
        }

        public string GetIconName()
        {
            if (Icon != null && Icon.Length > 0)
            {
                int lastSeparator = Icon.LastIndexOf('/');
                if (lastSeparator != -1)
                {
                    return Icon.Substring(lastSeparator + 1);
                }
            }

            return Icon;
        }

        public static implicit operator Zproto.SkillLevelInfo(SkillLevelInfo skillLevelInfo)
        {
            var data = new Zproto.SkillLevelInfo()
            {
                SkillId = skillLevelInfo.SkillId,
                CurrentLevel = skillLevelInfo.CurrentLevel,
                RemodelLevel = skillLevelInfo.Tier
            };
            return data;
        }
    }
}
