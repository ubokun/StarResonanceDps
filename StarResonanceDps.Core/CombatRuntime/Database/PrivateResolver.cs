// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace StarResonanceDps.Core.CombatRuntime.Database
{
    public class PrivateResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            if (!prop.Writable)
            {
                var property = member as PropertyInfo;
                var hasPrivateSetter = property?.GetSetMethod(true) != null;
                prop.Writable = hasPrivateSetter;
            }
            return prop;
        }
    }
}
