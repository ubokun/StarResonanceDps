// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.DataTypes.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class BuffTable
    {
        public Dictionary<string, Buff> Data = new();
    }

    public class Buff
    {
        public string Id { get; set; } = null!;
        public int Level { get; set; }
        public string NameDesign { get; set; } = null!;
        public string Note { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Icon { get; set; } = null!;
        public string Desc { get; set; } = null!;
        public Enum.EBuffType? BuffType { get; set; }
        public Enum.EBuffPriority? BuffPriority { get; set; }
        public int TipsDescription { get; set; }
        public int Visible { get; set; }
        public List<int> RepeatAddRule { get; set; } = null!;
        public List<List<float>> DestroyParam { get; set; } = null!;
        public bool DeleteDead { get; set; }
        public bool DeleteOffline { get; set; }
        public bool DeleteChangeScene { get; set; }
        public bool DeleteChangeVisualLayer { get; set; }
        public bool DeleteWeaponChange { get; set; }
        public bool DeleteSourceDead { get; set; }
        public List<int> Tags { get; set; } = null!;
        public List<int> SpecialAttr { get; set; } = null!;
        public int BuffAbilityType { get; set; }
        public int BuffAbilitySubType { get; set; }
        public bool IsClientBuff { get; set; }
        public string ShowHUDIcon { get; set; } = null!;
        public int HudSwitch { get; set; }
        public int TimeRefreshType { get; set; }
        public int PlayType { get; set; }
        public int SkillId { get; set; }

        /// <summary>
        /// 畳み先のバフ番号。手修正 <c>BuffOverrides.json</c> だけが書く項目で、表(JSON)には無い。
        /// 畳んだバフはバフ/デバフリストで畳み先と同じものとして出る(名前・絵・側・優先度・行とカードの鍵・非表示リストの番号)。
        /// 読み込みが畳み先の <see cref="BuffType"/>・<see cref="BuffPriority"/>・<see cref="Icon"/>・<see cref="ShowHUDIcon"/> を写す。
        /// </summary>
        public int? FoldInto { get; set; }

        public string GetIconName()
        {

            if (ShowHUDIcon != null && ShowHUDIcon.Length > 0)
            {
                int lastSeparator = ShowHUDIcon.LastIndexOf('/');
                if (lastSeparator != -1)
                {
                    return ShowHUDIcon.Substring(lastSeparator + 1);
                }
            }

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
    }
}
