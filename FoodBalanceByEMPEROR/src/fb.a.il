.class private auto ansi '<Module>'
{
} 
.class private auto ansi sealed beforefieldinit Microsoft.CodeAnalysis.EmbeddedAttribute
extends [System.Runtime]System.Attribute
{
.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
01 00 00 00
)
.custom instance void Microsoft.CodeAnalysis.EmbeddedAttribute::.ctor() = (
01 00 00 00
)
.method public hidebysig specialname rtspecialname 
instance void .ctor () cil managed 
{
.maxstack 8
IL: ldarg.0
IL: call instance void [System.Runtime]System.Attribute::.ctor()
IL: ret
} 
} 
.class private auto ansi sealed beforefieldinit System.Runtime.CompilerServices.RefSafetyRulesAttribute
extends [System.Runtime]System.Attribute
{
.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
01 00 00 00
)
.custom instance void Microsoft.CodeAnalysis.EmbeddedAttribute::.ctor() = (
01 00 00 00
)
.custom instance void [System.Runtime]System.AttributeUsageAttribute::.ctor(valuetype [System.Runtime]System.AttributeTargets) = (
01 00 02 00 00 00 02 00 54 02 0d 41 6c 6c 6f 77
4d 75 6c 74 69 70 6c 65 00 54 02 09 49 6e 68 65
72 69 74 65 64 00
)
.field public initonly int32 Version
.method public hidebysig specialname rtspecialname 
instance void .ctor (
int32 ''
) cil managed 
{
.maxstack 8
IL: ldarg.0
IL: call instance void [System.Runtime]System.Attribute::.ctor()
IL: ldarg.0
IL: ldarg.1
IL: stfld int32 System.Runtime.CompilerServices.RefSafetyRulesAttribute::Version
IL: ret
} 
} 
.class public auto ansi beforefieldinit FoodBalanceByEMPEROR.BalanceValues
extends [System.Runtime]System.Object
{
.field public float32 General
.field public float32 FoodPack
.field public float32 CalorieWaltz
.field public float32 RetroFood
.field public float32 AutumnComforts
.field public float32 Seagull
.field public float32 GameContainers
.field public float32 GameMaps
.method public hidebysig specialname rtspecialname 
instance void .ctor () cil managed 
{
.maxstack 2
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::General
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::FoodPack
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::CalorieWaltz
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::RetroFood
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::AutumnComforts
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::Seagull
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::GameContainers
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::GameMaps
IL: ldarg.0
IL: call instance void [System.Runtime]System.Object::.ctor()
IL: ret
} 
} 
.class private auto ansi beforefieldinit FoodBalanceByEMPEROR.BalanceSettings
extends [ModSettings]ModSettings.JsonModSettings
{
.field public float32 General
.custom instance void [ModSettings]ModSettings.SectionAttribute::.ctor(string) = (
01 00 17 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 53
45 43 54 49 4f 4e 5f 41 4c 4c 01 00 54 02 08 4c
6f 63 61 6c 69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 13 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 47
45 4e 45 52 41 4c 01 00 54 02 08 4c 6f 63 61 6c
69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 18 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 47
45 4e 45 52 41 4c 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 FoodPack
.custom instance void [ModSettings]ModSettings.SectionAttribute::.ctor(string) = (
01 00 18 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 53
45 43 54 49 4f 4e 5f 4d 4f 44 53 01 00 54 02 08
4c 6f 63 61 6c 69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 14 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 46
4f 4f 44 50 41 43 4b 01 00 54 02 08 4c 6f 63 61
6c 69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 14 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 4d
4f 44 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 CalorieWaltz
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 11 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 57
41 4c 54 5a 01 00 54 02 08 4c 6f 63 61 6c 69 7a
65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 14 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 4d
4f 44 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 RetroFood
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 11 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 52
45 54 52 4f 01 00 54 02 08 4c 6f 63 61 6c 69 7a
65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 14 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 4d
4f 44 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 AutumnComforts
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 12 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 41
55 54 55 4d 4e 01 00 54 02 08 4c 6f 63 61 6c 69
7a 65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 14 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 4d
4f 44 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 Seagull
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 13 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 53
45 41 47 55 4c 4c 01 00 54 02 08 4c 6f 63 61 6c
69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 18 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 53
45 41 47 55 4c 4c 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 GameContainers
.custom instance void [ModSettings]ModSettings.SectionAttribute::.ctor(string) = (
01 00 18 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 53
45 43 54 49 4f 4e 5f 47 41 4d 45 01 00 54 02 08
4c 6f 63 61 6c 69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 16 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 47
41 4d 45 5f 42 4f 58 45 53 01 00 54 02 08 4c 6f
63 61 6c 69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 1b 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 47
41 4d 45 5f 42 4f 58 45 53 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 48 43 15 00 00 00 00 00
)
.field public float32 GameMaps
.custom instance void [ModSettings]ModSettings.NameAttribute::.ctor(string) = (
01 00 15 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 47
41 4d 45 5f 4d 41 50 53 01 00 54 02 08 4c 6f 63
61 6c 69 7a 65 01
)
.custom instance void [ModSettings]ModSettings.DescriptionAttribute::.ctor(string) = (
01 00 1a 46 4f 4f 44 42 41 4c 41 4e 43 45 5f 47
41 4d 45 5f 4d 41 50 53 5f 44 45 53 43 00 00
)
.custom instance void [ModSettings]ModSettings.SliderAttribute::.ctor(float32, float32, int32) = (
01 00 00 00 00 00 00 00 c8 42 0b 00 00 00 00 00
)
.field private static class FoodBalanceByEMPEROR.BalanceSettings 'instance'
.method public hidebysig static 
void Load () cil managed 
{
.maxstack 8
IL: newobj instance void FoodBalanceByEMPEROR.BalanceSettings::.ctor()
IL: stsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldstr "Food Balance"
IL: callvirt instance void [ModSettings]ModSettings.ModSettingsBase::AddToModSettings(string)
IL: ret
} 
.method public hidebysig specialname static 
class FoodBalanceByEMPEROR.BalanceValues get_Values () cil managed 
{
.maxstack 3
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: brfalse IL
IL: newobj instance void FoodBalanceByEMPEROR.BalanceValues::.ctor()
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::General
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::General
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::FoodPack
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::FoodPack
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::CalorieWaltz
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::CalorieWaltz
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::RetroFood
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::RetroFood
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::AutumnComforts
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::AutumnComforts
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::Seagull
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::Seagull
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::GameContainers
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::GameContainers
IL: dup
IL: ldsfld class FoodBalanceByEMPEROR.BalanceSettings FoodBalanceByEMPEROR.BalanceSettings::'instance'
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceSettings::GameMaps
IL: stfld float32 FoodBalanceByEMPEROR.BalanceValues::GameMaps
IL: ret
IL: newobj instance void FoodBalanceByEMPEROR.BalanceValues::.ctor()
IL: ret
} 
.method public hidebysig specialname rtspecialname 
instance void .ctor () cil managed 
{
.maxstack 2
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::General
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::FoodPack
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::CalorieWaltz
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::RetroFood
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::AutumnComforts
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::Seagull
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::GameContainers
IL: ldarg.0
IL: ldc.r4 100
IL: stfld float32 FoodBalanceByEMPEROR.BalanceSettings::GameMaps
IL: ldarg.0
IL: call instance void [ModSettings]ModSettings.JsonModSettings::.ctor()
IL: ret
} 
.property class FoodBalanceByEMPEROR.BalanceValues Values()
{
.get class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.BalanceSettings::get_Values()
}
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.Words
extends [System.Runtime]System.Object
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string, class [System.Runtime]System.Type[]) = (
01 00 5b 49 6c 32 43 70 70 2e 4c 6f 63 61 6c 69
7a 61 74 69 6f 6e 2c 20 41 73 73 65 6d 62 6c 79
2d 43 53 68 61 72 70 2c 20 56 65 72 73 69 6f 6e
3d 30 2e 30 2e 30 2e 30 2c 20 43 75 6c 74 75 72
65 3d 6e 65 75 74 72 61 6c 2c 20 50 75 62 6c 69
63 4b 65 79 54 6f 6b 65 6e 3d 6e 75 6c 6c 03 47
65 74 01 00 00 00 60 53 79 73 74 65 6d 2e 53 74
72 69 6e 67 2c 20 53 79 73 74 65 6d 2e 52 75 6e
74 69 6d 65 2c 20 56 65 72 73 69 6f 6e 3d 36 2e
30 2e 30 2e 30 2c 20 43 75 6c 74 75 72 65 3d 6e
65 75 74 72 61 6c 2c 20 50 75 62 6c 69 63 4b 65
79 54 6f 6b 65 6e 3d 62 30 33 66 35 66 37 66 31
31 64 35 30 61 33 61 00 00
)
.field private static initonly class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>> Table
.custom instance void [System.Runtime]System.Runtime.CompilerServices.TupleElementNamesAttribute::.ctor(string[]) = (
01 00 02 00 00 00 02 72 75 02 65 6e 00 00
)
.method private hidebysig static 
void Postfix (
string key,
string& __result
) cil managed 
{
.maxstack 3
.locals init (
[0] valuetype [System.Runtime]System.ValueTuple`2<string, string>
)
IL: ldarg.0
IL: brfalse.s IL
IL: ldarg.0
IL: ldstr "FOODBALANCE_"
IL: callvirt instance bool [System.Runtime]System.String::StartsWith(string)
IL: brfalse.s IL
IL: ldsfld class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>> FoodBalanceByEMPEROR.Words::Table
IL: ldarg.0
IL: ldloca.s 0
IL: callvirt instance bool class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::TryGetValue(!0, !1&)
IL: brfalse.s IL
IL: ldarg.1
IL: call string ['Assembly-CSharp']Il2Cpp.Localization::get_Language()
IL: ldstr "Russian"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldloc.0
IL: ldfld !1 valuetype [System.Runtime]System.ValueTuple`2<string, string>::Item2
IL: br.s IL
IL: ldloc.0
IL: ldfld !0 valuetype [System.Runtime]System.ValueTuple`2<string, string>::Item1
IL: stind.ref
IL: ret
} 
.method private hidebysig specialname rtspecialname static 
void .cctor () cil managed 
{
.maxstack 5
IL: newobj instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::.ctor()
IL: dup
IL: ldstr "FOODBALANCE_SECTION_ALL"
IL: ldstr "Вся еда"
IL: ldstr "All food"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_GENERAL"
IL: ldstr "Общий множитель"
IL: ldstr "Overall"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_GENERAL_DESC"
IL: ldstr "Умножается на ползунок источника. Действует только на ещё не посещённые места и не заполненные контейнеры."
IL: ldstr "Multiplied by each source's slider. Applies only to places not yet visited and containers not yet filled."
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_SECTION_MODS"
IL: ldstr "Моды на еду"
IL: ldstr "Food mods"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_FOODPACK"
IL: ldstr "Food Pack by TKG"
IL: ldstr "Food Pack by TKG"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_WALTZ"
IL: ldstr "Foods by Calorie Waltz"
IL: ldstr "Foods by Calorie Waltz"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_RETRO"
IL: ldstr "Retro Food"
IL: ldstr "Retro Food"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_AUTUMN"
IL: ldstr "Autumn Comforts"
IL: ldstr "Autumn Comforts"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_MOD_DESC"
IL: ldstr "Сколько еды этого мода на картах и в контейнерах. На картах шанс не поднимается выше 100%, поэтому 200% дают немного меньше двойного количества."
IL: ldstr "How much of this mod's food on maps and in containers. On maps a chance stops at 100%, so 200% gives a little less than double."
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_SEAGULL"
IL: ldstr "Food Pack by EMPEROR"
IL: ldstr "Food Pack by EMPEROR"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_SEAGULL_DESC"
IL: ldstr "Сколько еды Food Pack by EMPEROR на картах и в контейнерах. Саке и карточки рецептов не меняются."
IL: ldstr "How much Food Pack by EMPEROR food on maps and in containers. Sake and recipe cards don't change."
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_SECTION_GAME"
IL: ldstr "Еда самой игры"
IL: ldstr "The game's own food"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_GAME_BOXES"
IL: ldstr "В контейнерах"
IL: ldstr "In containers"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_GAME_BOXES_DESC"
IL: ldstr "Сколько игровой еды в контейнерах: шкафах, рюкзаках, машинах. Вода не меняется."
IL: ldstr "How much of the game's food in containers: cupboards, backpacks, cars. Water doesn't change."
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_GAME_MAPS"
IL: ldstr "На картах"
IL: ldstr "On maps"
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: dup
IL: ldstr "FOODBALANCE_GAME_MAPS_DESC"
IL: ldstr "Сколько игровой еды лежит на картах. Только уменьшение: при первом посещении места часть еды убирается."
IL: ldstr "How much of the game's food lies on maps. Only less: part of it is removed on a place's first visit."
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<string, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>>::set_Item(!0, !1)
IL: stsfld class [System.Collections]System.Collections.Generic.Dictionary`2<string, valuetype [System.Runtime]System.ValueTuple`2<string, string>> FoodBalanceByEMPEROR.Words::Table
IL: ret
} 
} 
.class public auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.FoodBalance
extends [System.Runtime]System.Object
{
.class nested private auto ansi beforefieldinit Sources
extends [System.Runtime]System.Object
{
.field public class [System.Collections]System.Collections.Generic.Dictionary`2<string, string> Food
.field public class [System.Collections]System.Collections.Generic.HashSet`1<string> Protected
.method public hidebysig specialname rtspecialname 
instance void .ctor () cil managed 
{
.maxstack 8
IL: ldarg.0
IL: newobj instance void class [System.Collections]System.Collections.Generic.Dictionary`2<string, string>::.ctor()
IL: stfld class [System.Collections]System.Collections.Generic.Dictionary`2<string, string> FoodBalanceByEMPEROR.FoodBalance/Sources::Food
IL: ldarg.0
IL: newobj instance void class [System.Collections]System.Collections.Generic.HashSet`1<string>::.ctor()
IL: stfld class [System.Collections]System.Collections.Generic.HashSet`1<string> FoodBalanceByEMPEROR.FoodBalance/Sources::Protected
IL: ldarg.0
IL: call instance void [System.Runtime]System.Object::.ctor()
IL: ret
} 
} 
.class nested private auto ansi sealed serializable beforefieldinit '<>c'
extends [System.Runtime]System.Object
{
.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
01 00 00 00
)
.field public static initonly class FoodBalanceByEMPEROR.FoodBalance/'<>c' '<>9'
.field public static class [System.Runtime]System.Func`2<class [System.Runtime]System.Reflection.Assembly, bool> '<>9__25_0'
.method private hidebysig specialname rtspecialname static 
void .cctor () cil managed 
{
.maxstack 8
IL: newobj instance void FoodBalanceByEMPEROR.FoodBalance/'<>c'::.ctor()
IL: stsfld class FoodBalanceByEMPEROR.FoodBalance/'<>c' FoodBalanceByEMPEROR.FoodBalance/'<>c'::'<>9'
IL: ret
} 
.method public hidebysig specialname rtspecialname 
instance void .ctor () cil managed 
{
.maxstack 8
IL: ldarg.0
IL: call instance void [System.Runtime]System.Object::.ctor()
IL: ret
} 
.method assembly hidebysig 
instance bool '<ConnectFoodPack>b__25_0' (
class [System.Runtime]System.Reflection.Assembly a
) cil managed 
{
.maxstack 8
IL: ldarg.1
IL: callvirt instance class [System.Runtime]System.Reflection.AssemblyName [System.Runtime]System.Reflection.Assembly::GetName()
IL: callvirt instance string [System.Runtime]System.Reflection.AssemblyName::get_Name()
IL: ldstr "FoodPackByEMPEROR"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: ret
} 
} 
.field public static string SettingsOverride
.field private static string parsedFrom
.field private static class FoodBalanceByEMPEROR.BalanceValues parsed
.field private static class FoodBalanceByEMPEROR.FoodBalance/Sources sources
.method public hidebysig static 
string ExportSettings () cil managed 
{
.maxstack 8
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
IL: call string [Newtonsoft.Json]Newtonsoft.Json.JsonConvert::SerializeObject(object)
IL: ret
} 
.method public hidebysig specialname static 
bool get_WorldSpawnsPending () cil managed 
{
.maxstack 8
IL: call bool FoodBalanceByEMPEROR.GameFoodOnMaps::get_Pending()
IL: ret
} 
.method assembly hidebysig specialname static 
class FoodBalanceByEMPEROR.BalanceValues get_Values () cil managed 
{
.maxstack 2
.locals init (
[0] class [System.Runtime]System.Exception
)
IL: ldsfld string FoodBalanceByEMPEROR.FoodBalance::SettingsOverride
IL: brtrue.s IL
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.BalanceSettings::get_Values()
IL: ret
IL: ldsfld string FoodBalanceByEMPEROR.FoodBalance::parsedFrom
IL: ldsfld string FoodBalanceByEMPEROR.FoodBalance::SettingsOverride
IL: beq.s IL
IL: ldsfld string FoodBalanceByEMPEROR.FoodBalance::SettingsOverride
IL: stsfld string FoodBalanceByEMPEROR.FoodBalance::parsedFrom
.try
{
IL: ldsfld string FoodBalanceByEMPEROR.FoodBalance::SettingsOverride
IL: call !!0 [Newtonsoft.Json]Newtonsoft.Json.JsonConvert::DeserializeObject<class FoodBalanceByEMPEROR.BalanceValues>(string)
IL: stsfld class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::parsed
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.0
IL: ldstr "Settings from the host unreadable, using own: "
IL: ldloc.0
IL: callvirt instance string [System.Runtime]System.Exception::get_Message()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: ldnull
IL: stsfld class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::parsed
IL: leave.s IL
} 
IL: ldsfld class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::parsed
IL: dup
IL: brtrue.s IL
IL: pop
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.BalanceSettings::get_Values()
IL: ret
} 
.method assembly hidebysig specialname static 
int32 get_KnownFood () cil managed 
{
.maxstack 8
IL: ldsfld class FoodBalanceByEMPEROR.FoodBalance/Sources FoodBalanceByEMPEROR.FoodBalance::sources
IL: ldfld class [System.Collections]System.Collections.Generic.Dictionary`2<string, string> FoodBalanceByEMPEROR.FoodBalance/Sources::Food
IL: callvirt instance int32 class [System.Collections]System.Collections.Generic.Dictionary`2<string, string>::get_Count()
IL: ret
} 
.method public hidebysig static 
void Load (
string root
) cil managed 
{
.maxstack 2
.locals init (
[0] string
)
IL: ldarg.0
IL: ldstr "food_sources.json"
IL: call string [System.Runtime]System.IO.Path::Combine(string, string)
IL: stloc.0
IL: ldloc.0
IL: call bool [System.Runtime]System.IO.File::Exists(string)
IL: brfalse.s IL
IL: ldloc.0
IL: call string [System.Runtime]System.IO.File::ReadAllText(string)
IL: call !!0 [Newtonsoft.Json]Newtonsoft.Json.JsonConvert::DeserializeObject<class FoodBalanceByEMPEROR.FoodBalance/Sources>(string)
IL: dup
IL: brtrue.s IL
IL: pop
IL: newobj instance void FoodBalanceByEMPEROR.FoodBalance/Sources::.ctor()
IL: stsfld class FoodBalanceByEMPEROR.FoodBalance/Sources FoodBalanceByEMPEROR.FoodBalance::sources
IL: ret
} 
.method private hidebysig static 
string Key (
string name
) cil managed 
{
.maxstack 3
.locals init (
[0] int32
)
IL: ldarg.0
IL: ldc.i4.s 40
IL: callvirt instance int32 [System.Runtime]System.String::IndexOf(char)
IL: stloc.0
IL: ldloc.0
IL: ldc.i4.0
IL: ble.s IL
IL: ldarg.0
IL: ldc.i4.0
IL: ldloc.0
IL: callvirt instance string [System.Runtime]System.String::Substring(int32, int32)
IL: starg.s name
IL: ldarg.0
IL: callvirt instance string [System.Runtime]System.String::Trim()
IL: callvirt instance string [System.Runtime]System.String::ToLowerInvariant()
IL: starg.s name
IL: ldarg.0
IL: ldstr "gear_"
IL: callvirt instance bool [System.Runtime]System.String::StartsWith(string)
IL: brtrue.s IL
IL: ldarg.0
IL: ret
IL: ldarg.0
IL: ldc.i4.5
IL: callvirt instance string [System.Runtime]System.String::Substring(int32)
IL: ret
} 
.method private hidebysig static 
string SourceOf (
string name
) cil managed 
{
.maxstack 3
.locals init (
[0] string,
[1] string
)
IL: ldarg.0
IL: call string FoodBalanceByEMPEROR.FoodBalance::Key(string)
IL: stloc.0
IL: ldloc.0
IL: ldstr "seagull_"
IL: callvirt instance bool [System.Runtime]System.String::StartsWith(string)
IL: brfalse.s IL
IL: ldloc.0
IL: ldstr "seagull_sake_"
IL: callvirt instance bool [System.Runtime]System.String::StartsWith(string)
IL: brtrue.s IL
IL: ldloc.0
IL: ldstr "seagull_card_"
IL: callvirt instance bool [System.Runtime]System.String::StartsWith(string)
IL: brtrue.s IL
IL: ldstr "Seagull"
IL: ret
IL: ldnull
IL: ret
IL: ldsfld class FoodBalanceByEMPEROR.FoodBalance/Sources FoodBalanceByEMPEROR.FoodBalance::sources
IL: ldfld class [System.Collections]System.Collections.Generic.Dictionary`2<string, string> FoodBalanceByEMPEROR.FoodBalance/Sources::Food
IL: ldloc.0
IL: ldloca.s 1
IL: callvirt instance bool class [System.Collections]System.Collections.Generic.Dictionary`2<string, string>::TryGetValue(!0, !1&)
IL: brtrue.s IL
IL: ldnull
IL: ret
IL: ldloc.1
IL: ret
} 
.method private hidebysig static 
float32 Slider (
class FoodBalanceByEMPEROR.BalanceValues v,
string source
) cil managed 
{
.maxstack 2
.locals init (
[0] float32
)
IL: ldarg.1
IL: ldstr "FoodPack"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldarg.1
IL: ldstr "CalorieWaltz"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldarg.1
IL: ldstr "RetroFood"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldarg.1
IL: ldstr "AutumnComforts"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldarg.1
IL: ldstr "Seagull"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: br.s IL
IL: ldarg.0
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::FoodPack
IL: stloc.0
IL: br.s IL
IL: ldarg.0
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::CalorieWaltz
IL: stloc.0
IL: br.s IL
IL: ldarg.0
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::RetroFood
IL: stloc.0
IL: br.s IL
IL: ldarg.0
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::AutumnComforts
IL: stloc.0
IL: br.s IL
IL: ldarg.0
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::Seagull
IL: stloc.0
IL: br.s IL
IL: ldc.r4 100
IL: stloc.0
IL: ldloc.0
IL: ret
} 
.method assembly hidebysig static 
float32 InContainer (
string name
) cil managed 
{
.maxstack 3
.locals init (
[0] string,
[1] class FoodBalanceByEMPEROR.BalanceValues,
[2] float32
)
IL: ldarg.0
IL: call string FoodBalanceByEMPEROR.FoodBalance::SourceOf(string)
IL: stloc.0
IL: ldloc.0
IL: brtrue.s IL
IL: ldc.r4 1
IL: ret
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
IL: stloc.1
IL: ldloc.0
IL: ldstr "Game"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldloc.1
IL: ldloc.0
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::Slider(class FoodBalanceByEMPEROR.BalanceValues, string)
IL: br.s IL
IL: ldloc.1
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::GameContainers
IL: stloc.2
IL: ldloc.1
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::General
IL: ldloc.2
IL: mul
IL: ldc.r4 10000
IL: div
IL: ldc.r4 0.0
IL: ldc.r4 2
IL: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Clamp(float32, float32, float32)
IL: ret
} 
.method assembly hidebysig static 
float32 OnMap (
string name,
valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 'at'
) cil managed 
{
.maxstack 4
.locals init (
[0] string,
[1] class FoodBalanceByEMPEROR.BalanceValues,
[2] valuetype [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler
)
IL: ldarg.0
IL: call string FoodBalanceByEMPEROR.FoodBalance::SourceOf(string)
IL: stloc.0
IL: ldloc.0
IL: brtrue.s IL
IL: ldc.r4 1
IL: ret
IL: ldsfld class FoodBalanceByEMPEROR.FoodBalance/Sources FoodBalanceByEMPEROR.FoodBalance::sources
IL: ldfld class [System.Collections]System.Collections.Generic.HashSet`1<string> FoodBalanceByEMPEROR.FoodBalance/Sources::Protected
IL: ldloca.s 2
IL: ldc.i4.3
IL: ldc.i4.4
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: ldloca.s 2
IL: ldarg.0
IL: call string FoodBalanceByEMPEROR.FoodBalance::Key(string)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 2
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 2
IL: ldarg.1
IL: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
IL: ldc.r4 100
IL: mul
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::RoundToInt(float32)
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 2
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 2
IL: ldarg.1
IL: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
IL: ldc.r4 100
IL: mul
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::RoundToInt(float32)
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 2
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 2
IL: ldarg.1
IL: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
IL: ldc.r4 100
IL: mul
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::RoundToInt(float32)
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 2
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: callvirt instance bool class [System.Collections]System.Collections.Generic.HashSet`1<string>::Contains(!0)
IL: brfalse.s IL
IL: ldc.r4 1
IL: ret
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
IL: stloc.1
IL: ldloc.1
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::General
IL: ldloc.1
IL: ldloc.0
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::Slider(class FoodBalanceByEMPEROR.BalanceValues, string)
IL: mul
IL: ldc.r4 10000
IL: div
IL: ret
} 
.method private hidebysig static 
float32 ForFoodPack (
string name
) cil managed 
{
.maxstack 3
.locals init (
[0] string,
[1] class FoodBalanceByEMPEROR.BalanceValues
)
IL: ldarg.0
IL: call string FoodBalanceByEMPEROR.FoodBalance::SourceOf(string)
IL: stloc.0
IL: ldloc.0
IL: brtrue.s IL
IL: ldc.r4 1
IL: ret
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
IL: stloc.1
IL: ldloc.1
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::General
IL: ldloc.1
IL: ldloc.0
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::Slider(class FoodBalanceByEMPEROR.BalanceValues, string)
IL: mul
IL: ldc.r4 10000
IL: div
IL: ret
} 
.method assembly hidebysig specialname static 
float32 get_GameOnMaps () cil managed 
{
.maxstack 8
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::General
IL: call class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
IL: ldfld float32 FoodBalanceByEMPEROR.BalanceValues::GameMaps
IL: mul
IL: ldc.r4 10000
IL: div
IL: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Clamp01(float32)
IL: ret
} 
.method assembly hidebysig static 
bool IsGameFood (
string name
) cil managed 
{
.maxstack 8
IL: ldarg.0
IL: call string FoodBalanceByEMPEROR.FoodBalance::SourceOf(string)
IL: ldstr "Game"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: ret
} 
.method assembly hidebysig static 
float32 Roll (
string s
) cil managed 
{
.maxstack 2
.locals init (
[0] uint32,
[1] string,
[2] int32,
[3] char
)
IL: ldc.i4 -2128831035
IL: stloc.0
IL: ldarg.0
IL: stloc.1
IL: ldc.i4.0
IL: stloc.2
IL: br.s IL
IL: ldloc.1
IL: ldloc.2
IL: callvirt instance char [System.Runtime]System.String::get_Chars(int32)
IL: stloc.3
IL: ldloc.0
IL: ldloc.3
IL: xor
IL: ldc.i4 16777619
IL: mul
IL: stloc.0
IL: ldloc.2
IL: ldc.i4.1
IL: add
IL: stloc.2
IL: ldloc.2
IL: ldloc.1
IL: callvirt instance int32 [System.Runtime]System.String::get_Length()
IL: blt.s IL
IL: ldloc.0
IL: ldc.i4 100000
IL: rem.un
IL: conv.r.un
IL: conv.r4
IL: ldc.r4 100000
IL: div
IL: ret
} 
.method assembly hidebysig static 
string Text (
int32 n
) cil managed 
{
.maxstack 8
IL: ldarga.s n
IL: call class [System.Runtime]System.Globalization.CultureInfo [System.Runtime]System.Globalization.CultureInfo::get_InvariantCulture()
IL: call instance string [System.Runtime]System.Int32::ToString(class [System.Runtime]System.IFormatProvider)
IL: ret
} 
.method assembly hidebysig static 
string Cm (
valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 p
) cil managed 
{
.maxstack 3
.locals init (
[0] valuetype [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler
)
IL: ldloca.s 0
IL: ldc.i4.2
IL: ldc.i4.3
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: ldloca.s 0
IL: ldarg.0
IL: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
IL: ldc.r4 100
IL: mul
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::RoundToInt(float32)
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 0
IL: ldstr ","
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 0
IL: ldarg.0
IL: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
IL: ldc.r4 100
IL: mul
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::RoundToInt(float32)
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 0
IL: ldstr ","
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 0
IL: ldarg.0
IL: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
IL: ldc.r4 100
IL: mul
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::RoundToInt(float32)
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 0
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: ret
} 
.method assembly hidebysig static 
void ConnectFoodPack () cil managed 
{
.maxstack 4
.locals init (
[0] class [System.Runtime]System.Reflection.FieldInfo,
[1] class [System.Runtime]System.Exception
)
.try
{
IL: call class [System.Runtime]System.AppDomain [System.Runtime]System.AppDomain::get_CurrentDomain()
IL: callvirt instance class [System.Runtime]System.Reflection.Assembly[] [System.Runtime]System.AppDomain::GetAssemblies()
IL: ldsfld class [System.Runtime]System.Func`2<class [System.Runtime]System.Reflection.Assembly, bool> FoodBalanceByEMPEROR.FoodBalance/'<>c'::'<>9__25_0'
IL: dup
IL: brtrue.s IL
IL: pop
IL: ldsfld class FoodBalanceByEMPEROR.FoodBalance/'<>c' FoodBalanceByEMPEROR.FoodBalance/'<>c'::'<>9'
IL: ldftn instance bool FoodBalanceByEMPEROR.FoodBalance/'<>c'::'<ConnectFoodPack>b__25_0'(class [System.Runtime]System.Reflection.Assembly)
IL: newobj instance void class [System.Runtime]System.Func`2<class [System.Runtime]System.Reflection.Assembly, bool>::.ctor(object, native int)
IL: dup
IL: stsfld class [System.Runtime]System.Func`2<class [System.Runtime]System.Reflection.Assembly, bool> FoodBalanceByEMPEROR.FoodBalance/'<>c'::'<>9__25_0'
IL: call !!0 [System.Linq]System.Linq.Enumerable::FirstOrDefault<class [System.Runtime]System.Reflection.Assembly>(class [System.Runtime]System.Collections.Generic.IEnumerable`1<!!0>, class [System.Runtime]System.Func`2<!!0, bool>)
IL: dup
IL: brtrue.s IL
IL: pop
IL: ldnull
IL: br.s IL
IL: ldstr "FoodPackByEMPEROR.CoopApi"
IL: callvirt instance class [System.Runtime]System.Type [System.Runtime]System.Reflection.Assembly::GetType(string)
IL: dup
IL: brtrue.s IL
IL: pop
IL: ldnull
IL: br.s IL
IL: ldstr "AmountScale"
IL: ldc.i4.s 24
IL: callvirt instance class [System.Runtime]System.Reflection.FieldInfo [System.Runtime]System.Type::GetField(string, valuetype [System.Runtime]System.Reflection.BindingFlags)
IL: stloc.0
IL: ldloc.0
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.FieldInfo::op_Equality(class [System.Runtime]System.Reflection.FieldInfo, class [System.Runtime]System.Reflection.FieldInfo)
IL: brfalse.s IL
IL: leave.s IL
IL: ldloc.0
IL: ldnull
IL: ldnull
IL: ldftn float32 FoodBalanceByEMPEROR.FoodBalance::ForFoodPack(string)
IL: newobj instance void class [System.Runtime]System.Func`2<string, float32>::.ctor(object, native int)
IL: callvirt instance void [System.Runtime]System.Reflection.FieldInfo::SetValue(object, object)
IL: ldstr "Connected to FoodPackByEMPEROR."
IL: call void [MelonLoader]MelonLoader.MelonLogger::Msg(string)
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.1
IL: ldstr "FoodPackByEMPEROR: "
IL: ldloc.1
IL: callvirt instance string [System.Runtime]System.Exception::get_Message()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: leave.s IL
} 
IL: ret
} 
.method private hidebysig specialname rtspecialname static 
void .cctor () cil managed 
{
.maxstack 8
IL: newobj instance void FoodBalanceByEMPEROR.FoodBalance/Sources::.ctor()
IL: stsfld class FoodBalanceByEMPEROR.FoodBalance/Sources FoodBalanceByEMPEROR.FoodBalance::sources
IL: ret
} 
.property bool WorldSpawnsPending()
{
.get bool FoodBalanceByEMPEROR.FoodBalance::get_WorldSpawnsPending()
}
.property class FoodBalanceByEMPEROR.BalanceValues Values()
{
.get class FoodBalanceByEMPEROR.BalanceValues FoodBalanceByEMPEROR.FoodBalance::get_Values()
}
.property int32 KnownFood()
{
.get int32 FoodBalanceByEMPEROR.FoodBalance::get_KnownFood()
}
.property float32 GameOnMaps()
{
.get float32 FoodBalanceByEMPEROR.FoodBalance::get_GameOnMaps()
}
} 
.class public auto ansi beforefieldinit FoodBalanceByEMPEROR.Mod
extends [MelonLoader]MelonLoader.MelonMod
{
.field public static literal string VersionText = "1.0.6"
.method public hidebysig specialname static 
string get_Root () cil managed 
{
.maxstack 8
IL: call string [MelonLoader]MelonLoader.Utils.MelonEnvironment::get_ModsDirectory()
IL: ldstr "FoodBalanceByEMPEROR"
IL: call string [System.Runtime]System.IO.Path::Combine(string, string)
IL: ret
} 
.method public hidebysig virtual 
instance void OnInitializeMelon () cil managed 
{
.maxstack 4
.locals init (
[0] valuetype [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler
)
IL: call void FoodBalanceByEMPEROR.BalanceSettings::Load()
IL: call string FoodBalanceByEMPEROR.Mod::get_Root()
IL: call void FoodBalanceByEMPEROR.FoodBalance::Load(string)
IL: ldarg.0
IL: call instance class [MelonLoader]MelonLoader.MelonLogger/Instance [MelonLoader]MelonLoader.MelonBase::get_LoggerInstance()
IL: ldloca.s 0
IL: ldc.i4.s 22
IL: ldc.i4.1
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: ldloca.s 0
IL: ldstr "Loaded "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 0
IL: call int32 FoodBalanceByEMPEROR.FoodBalance::get_KnownFood()
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted<int32>(!!0)
IL: ldloca.s 0
IL: ldstr " kinds of food."
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 0
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: callvirt instance void [MelonLoader]MelonLoader.MelonLogger/Instance::Msg(string)
IL: ret
} 
.method public hidebysig virtual 
instance void OnLateInitializeMelon () cil managed 
{
.maxstack 8
IL: call void FoodBalanceByEMPEROR.FoodBalance::ConnectFoodPack()
IL: ret
} 
.method public hidebysig virtual 
instance void OnUpdate () cil managed 
{
.maxstack 8
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Tick()
IL: ret
} 
.method public hidebysig specialname rtspecialname 
instance void .ctor () cil managed 
{
.maxstack 8
IL: ldarg.0
IL: call instance void [MelonLoader]MelonLoader.MelonMod::.ctor()
IL: ret
} 
.property string Root()
{
.get string FoodBalanceByEMPEROR.Mod::get_Root()
}
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.ContainerPatch
extends [System.Runtime]System.Object
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 58 49 6c 32 43 70 70 2e 43 6f 6e 74 61 69
6e 65 72 2c 20 41 73 73 65 6d 62 6c 79 2d 43 53
68 61 72 70 2c 20 56 65 72 73 69 6f 6e 3d 30 2e
30 2e 30 2e 30 2c 20 43 75 6c 74 75 72 65 3d 6e
65 75 74 72 61 6c 2c 20 50 75 62 6c 69 63 4b 65
79 54 6f 6b 65 6e 3d 6e 75 6c 6c 16 50 6f 70 75
6c 61 74 65 57 69 74 68 52 61 6e 64 6f 6d 47 65
61 72 00 00
)
.method private hidebysig static 
void Prefix (
class ['Assembly-CSharp']Il2Cpp.Container __instance,
[out] int32& __state
) cil managed 
{
.maxstack 8
IL: ldarg.1
IL: ldarg.0
IL: callvirt instance class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string> ['Assembly-CSharp']Il2Cpp.Container::get_m_GearToInstantiate()
IL: dup
IL: brtrue.s IL
IL: pop
IL: ldc.i4.0
IL: br.s IL
IL: callvirt instance int32 class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string>::get_Count()
IL: stind.i4
IL: ret
} 
.method private hidebysig static 
void Postfix (
class ['Assembly-CSharp']Il2Cpp.Container __instance,
bool __runOriginal,
int32 __state
) cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPriority::.ctor(int32) = (
01 00 00 00 00 00 00 00
)
.maxstack 3
.locals init (
[0] class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string>,
[1] int32,
[2] string,
[3] class [System.Collections]System.Collections.Generic.List`1<string>,
[4] int32,
[5] int32,
[6] string,
[7] float32,
[8] float32,
[9] valuetype [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler,
[10] valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<string>,
[11] string,
[12] class [System.Runtime]System.Exception
)
IL: ldarg.1
IL: brtrue.s IL
IL: ret
IL: nop
.try
{
IL: ldarg.0
IL: callvirt instance class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string> ['Assembly-CSharp']Il2Cpp.Container::get_m_GearToInstantiate()
IL: stloc.0
IL: ldloc.0
IL: brtrue.s IL
IL: leave IL
IL: call class ['Assembly-CSharp']Il2Cpp.SceneTransitionData ['Assembly-CSharp']Il2Cpp.GameManager::get_m_SceneTransitionData()
IL: callvirt instance int32 ['Assembly-CSharp']Il2Cpp.SceneTransitionData::get_m_GameRandomSeed()
IL: stloc.1
IL: ldarg.0
IL: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
IL: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
IL: call string FoodBalanceByEMPEROR.FoodBalance::Cm(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
IL: stloc.2
IL: newobj instance void class [System.Collections]System.Collections.Generic.List`1<string>::.ctor()
IL: stloc.3
IL: ldc.i4.0
IL: stloc.s 4
IL: ldloc.0
IL: callvirt instance int32 class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string>::get_Count()
IL: ldc.i4.1
IL: sub
IL: stloc.s 5
IL: br IL
IL: ldloc.0
IL: ldloc.s 5
IL: callvirt instance !0 class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string>::get_Item(int32)
IL: stloc.s 6
IL: ldloc.s 6
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::InContainer(string)
IL: stloc.s 7
IL: ldloc.s 7
IL: ldc.r4 1
IL: beq IL
IL: ldc.i4.s 10
IL: ldc.i4.3
IL: newobj instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: stloc.s 9
IL: ldloca.s 9
IL: ldloc.1
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 9
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 9
IL: ldloc.2
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 9
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 9
IL: ldloc.s 5
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 9
IL: ldstr "|balance"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 9
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::Roll(string)
IL: stloc.s 8
IL: ldloc.s 7
IL: ldc.r4 1
IL: bge.un.s IL
IL: ldloc.s 8
IL: ldloc.s 7
IL: blt.un.s IL
IL: ldloc.0
IL: ldloc.s 5
IL: callvirt instance void class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string>::RemoveAt(int32)
IL: ldloc.s 4
IL: ldc.i4.1
IL: add
IL: stloc.s 4
IL: br.s IL
IL: ldloc.s 7
IL: ldc.r4 1
IL: ble.un.s IL
IL: ldloc.s 8
IL: ldloc.s 7
IL: ldc.r4 1
IL: sub
IL: bge.un.s IL
IL: ldloc.3
IL: ldloc.s 6
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<string>::Add(!0)
IL: ldloc.s 5
IL: ldc.i4.1
IL: sub
IL: stloc.s 5
IL: ldloc.s 5
IL: ldarg.2
IL: bge IL
IL: ldloc.3
IL: callvirt instance valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<!0> class [System.Collections]System.Collections.Generic.List`1<string>::GetEnumerator()
IL: stloc.s 10
.try
{
IL: br.s IL
IL: ldloca.s 10
IL: call instance !0 valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<string>::get_Current()
IL: stloc.s 11
IL: ldloc.0
IL: ldloc.s 11
IL: callvirt instance void class [Il2Cppmscorlib]Il2CppSystem.Collections.Generic.List`1<string>::Add(!0)
IL: ldloca.s 10
IL: call instance bool valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<string>::MoveNext()
IL: brtrue.s IL
IL: leave.s IL
} 
finally
{
IL: ldloca.s 10
IL: constrained. valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<string>
IL: callvirt instance void [System.Runtime]System.IDisposable::Dispose()
IL: endfinally
} 
IL: ldloc.s 4
IL: ldc.i4.0
IL: bgt.s IL
IL: ldloc.3
IL: callvirt instance int32 class [System.Collections]System.Collections.Generic.List`1<string>::get_Count()
IL: ldc.i4.0
IL: ble.s IL
IL: ldc.i4.s 31
IL: ldc.i4.3
IL: newobj instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: stloc.s 9
IL: ldloca.s 9
IL: ldstr "Container at "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 9
IL: ldloc.2
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 9
IL: ldstr ": removed "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 9
IL: ldloc.s 4
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted<int32>(!!0)
IL: ldloca.s 9
IL: ldstr ", added "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 9
IL: ldloc.3
IL: callvirt instance int32 class [System.Collections]System.Collections.Generic.List`1<string>::get_Count()
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted<int32>(!!0)
IL: ldloca.s 9
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: call void [MelonLoader]MelonLoader.MelonLogger::Msg(string)
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.s 12
IL: ldstr "Container: "
IL: ldloc.s 12
IL: dup
IL: brtrue.s IL
IL: pop
IL: ldnull
IL: br.s IL
IL: callvirt instance string [System.Runtime]System.Object::ToString()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: leave.s IL
} 
IL: ret
} 
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.SpawnChancePatch
extends [System.Runtime]System.Object
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor() = (
01 00 00 00
)
.field private static class [System.Runtime]System.Reflection.MethodBase target
.field private static class [System.Runtime]System.Reflection.PropertyInfo prefabName
.field private static class [System.Runtime]System.Reflection.PropertyInfo position
.method private hidebysig static 
bool Prepare () cil managed 
{
.maxstack 3
.locals init (
[0] class [System.Runtime]System.Type
)
IL: ldstr "GearSpawner.DefaultGearSpawnHandler:GetProbability"
IL: ldnull
IL: ldnull
IL: call class [System.Runtime]System.Reflection.MethodInfo ['0Harmony']HarmonyLib.AccessTools::Method(string, class [System.Runtime]System.Type[], class [System.Runtime]System.Type[])
IL: stsfld class [System.Runtime]System.Reflection.MethodBase FoodBalanceByEMPEROR.SpawnChancePatch::target
IL: ldstr "GearSpawner.GearSpawnInfo"
IL: call class [System.Runtime]System.Type ['0Harmony']HarmonyLib.AccessTools::TypeByName(string)
IL: stloc.0
IL: ldloc.0
IL: ldnull
IL: call bool [System.Runtime]System.Type::op_Equality(class [System.Runtime]System.Type, class [System.Runtime]System.Type)
IL: brtrue.s IL
IL: ldloc.0
IL: ldstr "PrefabName"
IL: call class [System.Runtime]System.Reflection.PropertyInfo ['0Harmony']HarmonyLib.AccessTools::Property(class [System.Runtime]System.Type, string)
IL: br.s IL
IL: ldnull
IL: stsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::prefabName
IL: ldloc.0
IL: ldnull
IL: call bool [System.Runtime]System.Type::op_Equality(class [System.Runtime]System.Type, class [System.Runtime]System.Type)
IL: brtrue.s IL
IL: ldloc.0
IL: ldstr "Position"
IL: call class [System.Runtime]System.Reflection.PropertyInfo ['0Harmony']HarmonyLib.AccessTools::Property(class [System.Runtime]System.Type, string)
IL: br.s IL
IL: ldnull
IL: stsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::position
IL: ldsfld class [System.Runtime]System.Reflection.MethodBase FoodBalanceByEMPEROR.SpawnChancePatch::target
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.MethodBase::op_Equality(class [System.Runtime]System.Reflection.MethodBase, class [System.Runtime]System.Reflection.MethodBase)
IL: brtrue.s IL
IL: ldsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::prefabName
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.PropertyInfo::op_Equality(class [System.Runtime]System.Reflection.PropertyInfo, class [System.Runtime]System.Reflection.PropertyInfo)
IL: brtrue.s IL
IL: ldsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::position
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.PropertyInfo::op_Equality(class [System.Runtime]System.Reflection.PropertyInfo, class [System.Runtime]System.Reflection.PropertyInfo)
IL: brfalse.s IL
IL: ldstr "ModComponent's spawn chance not found: mods' food on maps won't change."
IL: call void [MelonLoader]MelonLoader.MelonLogger::Warning(string)
IL: ldsfld class [System.Runtime]System.Reflection.MethodBase FoodBalanceByEMPEROR.SpawnChancePatch::target
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.MethodBase::op_Inequality(class [System.Runtime]System.Reflection.MethodBase, class [System.Runtime]System.Reflection.MethodBase)
IL: brfalse.s IL
IL: ldsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::prefabName
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.PropertyInfo::op_Inequality(class [System.Runtime]System.Reflection.PropertyInfo, class [System.Runtime]System.Reflection.PropertyInfo)
IL: brfalse.s IL
IL: ldsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::position
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.PropertyInfo::op_Inequality(class [System.Runtime]System.Reflection.PropertyInfo, class [System.Runtime]System.Reflection.PropertyInfo)
IL: ret
IL: ldc.i4.0
IL: ret
} 
.method private hidebysig static 
class [System.Runtime]System.Reflection.MethodBase TargetMethod () cil managed 
{
.maxstack 8
IL: ldsfld class [System.Runtime]System.Reflection.MethodBase FoodBalanceByEMPEROR.SpawnChancePatch::target
IL: ret
} 
.method private hidebysig static 
void Postfix (
float32& __result,
object[] __args
) cil managed 
{
.maxstack 4
.locals init (
[0] object,
[1] float32,
[2] class [System.Runtime]System.Exception
)
.try
{
IL: ldarg.1
IL: ldc.i4.2
IL: ldelem.ref
IL: stloc.0
IL: ldsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::prefabName
IL: ldloc.0
IL: callvirt instance object [System.Runtime]System.Reflection.PropertyInfo::GetValue(object)
IL: castclass [System.Runtime]System.String
IL: ldsfld class [System.Runtime]System.Reflection.PropertyInfo FoodBalanceByEMPEROR.SpawnChancePatch::position
IL: ldloc.0
IL: callvirt instance object [System.Runtime]System.Reflection.PropertyInfo::GetValue(object)
IL: unbox.any [UnityEngine.CoreModule]UnityEngine.Vector3
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::OnMap(string, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
IL: stloc.1
IL: ldloc.1
IL: ldc.r4 1
IL: beq.s IL
IL: ldarg.0
IL: ldarg.0
IL: ldind.r4
IL: ldloc.1
IL: mul
IL: ldc.r4 0.0
IL: ldc.r4 100
IL: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Clamp(float32, float32, float32)
IL: stind.r4
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.2
IL: ldstr "Spawn chance: "
IL: ldloc.2
IL: callvirt instance string [System.Runtime]System.Exception::get_Message()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: leave.s IL
} 
IL: ret
} 
} 
.class public auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.GameFoodOnMaps
extends [System.Runtime]System.Object
{
.field private static bool decided
.field private static int32 fallbackFrame
.field private static string pendingKey
.field private static int32 pendingFrame
.field private static class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> candidates
.custom instance void [System.Runtime]System.Runtime.CompilerServices.TupleElementNamesAttribute::.ctor(string[]) = (
01 00 02 00 00 00 04 67 65 61 72 05 70 6c 61 63
65 00 00
)
.method assembly hidebysig specialname static 
bool get_Pending () cil managed 
{
.maxstack 8
IL: ldsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldc.i4.0
IL: bge.s IL
IL: ldsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: ldnull
IL: cgt.un
IL: ret
IL: ldc.i4.1
IL: ret
} 
.method assembly hidebysig static 
void Reset () cil managed 
{
.maxstack 8
IL: ldc.i4.0
IL: stsfld bool FoodBalanceByEMPEROR.GameFoodOnMaps::decided
IL: ldc.i4.m1
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldnull
IL: stsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Clear()
IL: ret
} 
.method assembly hidebysig static 
void Ready () cil managed 
{
.maxstack 8
IL: ldsfld bool FoodBalanceByEMPEROR.GameFoodOnMaps::decided
IL: brtrue.s IL
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Time::get_frameCount()
IL: ldc.i4.s 30
IL: add
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ret
} 
.method assembly hidebysig static 
void Read (
bool hadSave,
string scene
) cil managed 
{
.maxstack 2
.locals init (
[0] class [System.Runtime]System.Exception
)
IL: ldsfld bool FoodBalanceByEMPEROR.GameFoodOnMaps::decided
IL: brfalse.s IL
IL: ret
IL: ldc.i4.1
IL: stsfld bool FoodBalanceByEMPEROR.GameFoodOnMaps::decided
IL: ldc.i4.m1
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldarg.0
IL: brtrue.s IL
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::get_GameOnMaps()
IL: ldc.r4 1
IL: blt.un.s IL
IL: ret
IL: nop
.try
{
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Collect()
IL: ldarg.1
IL: call bool [System.Runtime]System.String::IsNullOrEmpty(string)
IL: brtrue.s IL
IL: ldarg.1
IL: br.s IL
IL: call string FoodBalanceByEMPEROR.GameFoodOnMaps::SaveKey()
IL: stsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Time::get_frameCount()
IL: ldc.i4.s 60
IL: add
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::pendingFrame
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.0
IL: ldstr "Game food on maps: "
IL: ldloc.0
IL: callvirt instance string [System.Runtime]System.Exception::get_Message()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Clear()
IL: leave.s IL
} 
IL: ret
} 
.method private hidebysig static 
void Collect () cil managed 
{
.maxstack 3
.locals init (
[0] class [System.Runtime]System.Collections.Generic.IEnumerator`1<class ['Assembly-CSharp']Il2Cpp.GearItem>,
[1] class ['Assembly-CSharp']Il2Cpp.GearItem
)
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Clear()
IL: call class [Il2CppInterop.Runtime]Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase`1<!!0> [UnityEngine.CoreModule]UnityEngine.Object::FindObjectsOfType<class ['Assembly-CSharp']Il2Cpp.GearItem>()
IL: callvirt instance class [System.Runtime]System.Collections.Generic.IEnumerator`1<!0> class [Il2CppInterop.Runtime]Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase`1<class ['Assembly-CSharp']Il2Cpp.GearItem>::GetEnumerator()
IL: stloc.0
.try
{
IL: br.s IL
IL: ldloc.0
IL: callvirt instance !0 class [System.Runtime]System.Collections.Generic.IEnumerator`1<class ['Assembly-CSharp']Il2Cpp.GearItem>::get_Current()
IL: stloc.1
IL: ldloc.1
IL: callvirt instance bool ['Assembly-CSharp']Il2Cpp.GearItem::get_m_InPlayerInventory()
IL: brtrue.s IL
IL: ldloc.1
IL: callvirt instance bool ['Assembly-CSharp']Il2Cpp.GearItem::get_m_InsideContainer()
IL: brtrue.s IL
IL: ldloc.1
IL: callvirt instance bool ['Assembly-CSharp']Il2Cpp.GearItem::get_m_BeenInPlayerInventory()
IL: brtrue.s IL
IL: ldloc.1
IL: callvirt instance class ['Assembly-CSharp']Il2Cpp.MissionObjectIdentifier ['Assembly-CSharp']Il2Cpp.GearItem::get_m_MissionObject()
IL: ldnull
IL: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
IL: brtrue.s IL
IL: ldloc.1
IL: callvirt instance class ['Assembly-CSharp']Il2Cpp.NarrativeCollectibleItem ['Assembly-CSharp']Il2Cpp.GearItem::get_m_NarrativeCollectibleItem()
IL: ldnull
IL: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
IL: brtrue.s IL
IL: ldloc.1
IL: callvirt instance string [UnityEngine.CoreModule]UnityEngine.Object::get_name()
IL: call bool FoodBalanceByEMPEROR.FoodBalance::IsGameFood(string)
IL: brfalse.s IL
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: ldloc.1
IL: ldloc.1
IL: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
IL: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
IL: call string FoodBalanceByEMPEROR.FoodBalance::Cm(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
IL: newobj instance void valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>::.ctor(!0, !1)
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Add(!0)
IL: ldloc.0
IL: callvirt instance bool [System.Runtime]System.Collections.IEnumerator::MoveNext()
IL: brtrue.s IL
IL: leave.s IL
} 
finally
{
IL: ldloc.0
IL: brfalse.s IL
IL: ldloc.0
IL: callvirt instance void [System.Runtime]System.IDisposable::Dispose()
IL: endfinally
} 
IL: ret
} 
.method assembly hidebysig static 
void BeforeSave () cil managed 
{
.maxstack 2
.locals init (
[0] string,
[1] string,
[2] class [System.Runtime]System.Exception
)
.try
{
IL: ldsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldc.i4.0
IL: blt.s IL
IL: call string FoodBalanceByEMPEROR.GameFoodOnMaps::SaveKey()
IL: stloc.0
IL: call string ['Assembly-CSharp']Il2Cpp.SaveGameSystem::GetCurrentSaveName()
IL: stloc.1
IL: ldloc.1
IL: call bool [System.Runtime]System.String::IsNullOrEmpty(string)
IL: brtrue.s IL
IL: ldloc.1
IL: ldloc.0
IL: call bool ['Assembly-CSharp']Il2Cpp.SaveGameSlots::HasFilenameInSlot(string, string)
IL: br.s IL
IL: ldc.i4.0
IL: ldloc.0
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Read(bool, string)
IL: ldsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: brfalse.s IL
IL: ldsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: ldnull
IL: stsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Thin(string)
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.2
IL: ldstr "Game food before saving: "
IL: ldloc.2
IL: callvirt instance string [System.Runtime]System.Exception::get_Message()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: ldc.i4.m1
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldnull
IL: stsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Clear()
IL: leave.s IL
} 
IL: ret
} 
.method private hidebysig static 
string SaveKey () cil managed 
{
.maxstack 1
.locals init (
[0] string
)
IL: call class ['Assembly-CSharp']Il2Cpp.SceneTransitionData ['Assembly-CSharp']Il2Cpp.GameManager::get_m_SceneTransitionData()
IL: callvirt instance string ['Assembly-CSharp']Il2Cpp.SceneTransitionData::get_m_SceneSaveFilenameCurrent()
IL: stloc.0
IL: ldloc.0
IL: call bool [System.Runtime]System.String::IsNullOrEmpty(string)
IL: brtrue.s IL
IL: ldloc.0
IL: ret
IL: call string ['Assembly-CSharp']Il2Cpp.GameManager::get_m_ActiveScene()
IL: ret
} 
.method private hidebysig specialname static 
bool get_Busy () cil managed 
{
.maxstack 8
IL: call bool ['Assembly-CSharp']Il2Cpp.SaveGameSystem::IsRestoreInProgress()
IL: brtrue.s IL
IL: call bool ['Assembly-CSharp']Il2Cpp.SaveGameSystem::IsSceneRestoreInProgress()
IL: brtrue.s IL
IL: call bool ['Assembly-CSharp']Il2Cpp.SceneManager::IsLoading()
IL: ret
IL: ldc.i4.1
IL: ret
} 
.method public hidebysig static 
void Tick () cil managed 
{
.maxstack 2
.locals init (
[0] string,
[1] string,
[2] class [System.Runtime]System.Exception
)
.try
{
IL: ldsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldc.i4.0
IL: blt.s IL
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Time::get_frameCount()
IL: ldsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: blt.s IL
IL: call bool FoodBalanceByEMPEROR.GameFoodOnMaps::get_Busy()
IL: brtrue.s IL
IL: call string FoodBalanceByEMPEROR.GameFoodOnMaps::SaveKey()
IL: stloc.0
IL: call string ['Assembly-CSharp']Il2Cpp.SaveGameSystem::GetCurrentSaveName()
IL: stloc.1
IL: ldloc.1
IL: call bool [System.Runtime]System.String::IsNullOrEmpty(string)
IL: brtrue.s IL
IL: ldloc.1
IL: ldloc.0
IL: call bool ['Assembly-CSharp']Il2Cpp.SaveGameSlots::HasFilenameInSlot(string, string)
IL: br.s IL
IL: ldc.i4.0
IL: ldloc.0
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Read(bool, string)
IL: ldsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: brfalse.s IL
IL: call int32 [UnityEngine.CoreModule]UnityEngine.Time::get_frameCount()
IL: ldsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::pendingFrame
IL: blt.s IL
IL: call bool FoodBalanceByEMPEROR.GameFoodOnMaps::get_Busy()
IL: brtrue.s IL
IL: ldsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: ldnull
IL: stsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Thin(string)
IL: leave.s IL
} 
catch [System.Runtime]System.Exception
{
IL: stloc.2
IL: ldstr "Game food on maps: "
IL: ldloc.2
IL: callvirt instance string [System.Runtime]System.Exception::get_Message()
IL: call string [System.Runtime]System.String::Concat(string, string)
IL: call void [MelonLoader]MelonLoader.MelonLogger::Error(string)
IL: ldc.i4.m1
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: ldnull
IL: stsfld string FoodBalanceByEMPEROR.GameFoodOnMaps::pendingKey
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Clear()
IL: leave.s IL
} 
IL: ret
} 
.method private hidebysig static 
void Thin (
string saveKey
) cil managed 
{
.maxstack 3
.locals init (
[0] float32,
[1] int32,
[2] int32,
[3] int32,
[4] valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>,
[5] class ['Assembly-CSharp']Il2Cpp.GearItem,
[6] string,
[7] valuetype [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler
)
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::get_GameOnMaps()
IL: stloc.0
IL: call class ['Assembly-CSharp']Il2Cpp.SceneTransitionData ['Assembly-CSharp']Il2Cpp.GameManager::get_m_SceneTransitionData()
IL: callvirt instance int32 ['Assembly-CSharp']Il2Cpp.SceneTransitionData::get_m_GameRandomSeed()
IL: stloc.1
IL: ldc.i4.0
IL: stloc.2
IL: ldc.i4.0
IL: stloc.3
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<!0> class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::GetEnumerator()
IL: stloc.s 4
.try
{
IL: br IL
IL: ldloca.s 4
IL: call instance !0 valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::get_Current()
IL: dup
IL: ldfld !0 valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>::Item1
IL: stloc.s 5
IL: ldfld !1 valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>::Item2
IL: stloc.s 6
IL: ldloc.s 5
IL: ldnull
IL: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
IL: brtrue IL
IL: ldloc.s 5
IL: callvirt instance bool ['Assembly-CSharp']Il2Cpp.GearItem::get_m_InPlayerInventory()
IL: brtrue.s IL
IL: ldloc.s 5
IL: callvirt instance bool ['Assembly-CSharp']Il2Cpp.GearItem::get_m_InsideContainer()
IL: brtrue.s IL
IL: ldloc.2
IL: ldc.i4.1
IL: add
IL: stloc.2
IL: ldc.i4.s 10
IL: ldc.i4.3
IL: newobj instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: stloc.s 7
IL: ldloca.s 7
IL: ldloc.1
IL: call string FoodBalanceByEMPEROR.FoodBalance::Text(int32)
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 7
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 7
IL: ldarg.0
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 7
IL: ldstr "|"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 7
IL: ldloc.s 6
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 7
IL: ldstr "|balance"
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 7
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: call float32 FoodBalanceByEMPEROR.FoodBalance::Roll(string)
IL: ldloc.0
IL: blt.s IL
IL: ldloc.s 5
IL: callvirt instance class [UnityEngine.CoreModule]UnityEngine.GameObject [UnityEngine.CoreModule]UnityEngine.Component::get_gameObject()
IL: call void [UnityEngine.CoreModule]UnityEngine.Object::Destroy(class [UnityEngine.CoreModule]UnityEngine.Object)
IL: ldloc.3
IL: ldc.i4.1
IL: add
IL: stloc.3
IL: ldloca.s 4
IL: call instance bool valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::MoveNext()
IL: brtrue IL
IL: leave.s IL
} 
finally
{
IL: ldloca.s 4
IL: constrained. valuetype [System.Collections]System.Collections.Generic.List`1/Enumerator<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>
IL: callvirt instance void [System.Runtime]System.IDisposable::Dispose()
IL: endfinally
} 
IL: ldsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: callvirt instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::Clear()
IL: ldloca.s 7
IL: ldc.i4.s 27
IL: ldc.i4.3
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(int32, int32)
IL: ldloca.s 7
IL: ldstr "Game food in "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 7
IL: ldarg.0
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(string)
IL: ldloca.s 7
IL: ldstr ": removed "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 7
IL: ldloc.3
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted<int32>(!!0)
IL: ldloca.s 7
IL: ldstr " of "
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(string)
IL: ldloca.s 7
IL: ldloc.2
IL: call instance void [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted<int32>(!!0)
IL: ldloca.s 7
IL: call instance string [System.Runtime]System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()
IL: call void [MelonLoader]MelonLoader.MelonLogger::Msg(string)
IL: ret
} 
.method private hidebysig specialname rtspecialname static 
void .cctor () cil managed 
{
.maxstack 8
IL: ldc.i4.m1
IL: stsfld int32 FoodBalanceByEMPEROR.GameFoodOnMaps::fallbackFrame
IL: newobj instance void class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>>::.ctor()
IL: stsfld class [System.Collections]System.Collections.Generic.List`1<valuetype [System.Runtime]System.ValueTuple`2<class ['Assembly-CSharp']Il2Cpp.GearItem, string>> FoodBalanceByEMPEROR.GameFoodOnMaps::candidates
IL: ret
} 
.property bool Pending()
{
.get bool FoodBalanceByEMPEROR.GameFoodOnMaps::get_Pending()
}
.property bool Busy()
{
.get bool FoodBalanceByEMPEROR.GameFoodOnMaps::get_Busy()
}
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.SceneVisitPatches
extends [System.Runtime]System.Object
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor() = (
01 00 00 00
)
.method private hidebysig static 
void ResetLoad () cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPrefix::.ctor() = (
01 00 00 00
)
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 5a 49 6c 32 43 70 70 2e 47 61 6d 65 4d 61
6e 61 67 65 72 2c 20 41 73 73 65 6d 62 6c 79 2d
43 53 68 61 72 70 2c 20 56 65 72 73 69 6f 6e 3d
30 2e 30 2e 30 2e 30 2c 20 43 75 6c 74 75 72 65
3d 6e 65 75 74 72 61 6c 2c 20 50 75 62 6c 69 63
4b 65 79 54 6f 6b 65 6e 3d 6e 75 6c 6c 1a 4c 6f
61 64 53 63 65 6e 65 57 69 74 68 4c 6f 61 64 69
6e 67 53 63 72 65 65 6e 00 00
)
.maxstack 8
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Reset()
IL: ret
} 
.method private hidebysig static 
void ResetRestore () cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPrefix::.ctor() = (
01 00 00 00
)
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 5d 49 6c 32 43 70 70 2e 53 61 76 65 47 61
6d 65 53 79 73 74 65 6d 2c 20 41 73 73 65 6d 62
6c 79 2d 43 53 68 61 72 70 2c 20 56 65 72 73 69
6f 6e 3d 30 2e 30 2e 30 2e 30 2c 20 43 75 6c 74
75 72 65 3d 6e 65 75 74 72 61 6c 2c 20 50 75 62
6c 69 63 4b 65 79 54 6f 6b 65 6e 3d 6e 75 6c 6c
0b 52 65 73 74 6f 72 65 47 61 6d 65 00 00
)
.maxstack 8
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Reset()
IL: ret
} 
.method private hidebysig static 
void BeforeSave () cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPrefix::.ctor() = (
01 00 00 00
)
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 5d 49 6c 32 43 70 70 2e 53 61 76 65 47 61
6d 65 53 79 73 74 65 6d 2c 20 41 73 73 65 6d 62
6c 79 2d 43 53 68 61 72 70 2c 20 56 65 72 73 69
6f 6e 3d 30 2e 30 2e 30 2e 30 2c 20 43 75 6c 74
75 72 65 3d 6e 65 75 74 72 61 6c 2c 20 50 75 62
6c 69 63 4b 65 79 54 6f 6b 65 6e 3d 6e 75 6c 6c
0d 53 61 76 65 53 63 65 6e 65 44 61 74 61 00 00
)
.maxstack 8
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::BeforeSave()
IL: ret
} 
.method private hidebysig static 
void Read (
bool __result,
string sceneSaveName
) cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPostfix::.ctor() = (
01 00 00 00
)
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 5d 49 6c 32 43 70 70 2e 53 61 76 65 47 61
6d 65 53 79 73 74 65 6d 2c 20 41 73 73 65 6d 62
6c 79 2d 43 53 68 61 72 70 2c 20 56 65 72 73 69
6f 6e 3d 30 2e 30 2e 30 2e 30 2c 20 43 75 6c 74
75 72 65 3d 6e 65 75 74 72 61 6c 2c 20 50 75 62
6c 69 63 4b 65 79 54 6f 6b 65 6e 3d 6e 75 6c 6c
0d 4c 6f 61 64 53 63 65 6e 65 44 61 74 61 00 00
)
.maxstack 8
IL: ldarg.0
IL: ldarg.1
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Read(bool, string)
IL: ret
} 
.method private hidebysig static 
void Ready () cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPostfix::.ctor() = (
01 00 00 00
)
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 68 49 6c 32 43 70 70 2e 49 6e 61 63 63 65
73 73 69 62 6c 65 47 65 61 72 43 6f 6e 74 61 69
6e 65 72 2c 20 41 73 73 65 6d 62 6c 79 2d 43 53
68 61 72 70 2c 20 56 65 72 73 69 6f 6e 3d 30 2e
30 2e 30 2e 30 2c 20 43 75 6c 74 75 72 65 3d 6e
65 75 74 72 61 6c 2c 20 50 75 62 6c 69 63 4b 65
79 54 6f 6b 65 6e 3d 6e 75 6c 6c 1c 49 6e 69 74
69 61 6c 69 7a 65 41 6c 6c 46 6f 72 43 75 72 72
65 6e 74 53 63 65 6e 65 00 00
)
.maxstack 8
IL: call void FoodBalanceByEMPEROR.GameFoodOnMaps::Ready()
IL: ret
} 
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.HintLanguage
extends [System.Runtime]System.Object
{
.method assembly hidebysig static 
void Fix (
class ['Assembly-CSharp']Il2Cpp.UILabel label
) cil managed 
{
.maxstack 4
.locals init (
[0] string,
[1] string[],
[2] bool,
[3] int32,
[4] string
)
IL: ldarg.0
IL: ldnull
IL: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
IL: brfalse.s IL
IL: ret
IL: ldarg.0
IL: callvirt instance string ['Assembly-CSharp']Il2Cpp.UILabel::get_text()
IL: stloc.0
IL: ldloc.0
IL: call bool [System.Runtime]System.String::IsNullOrEmpty(string)
IL: brfalse.s IL
IL: ret
IL: ldc.i4.s 10
IL: newarr [System.Runtime]System.String
IL: dup
IL: ldc.i4.0
IL: ldstr "Умножается на ползунок источника. Действует только на ещё не посещённые места и не заполненные контейнеры."
IL: stelem.ref
IL: dup
IL: ldc.i4.1
IL: ldstr "Multiplied by each source's slider. Applies only to places not yet visited and containers not yet filled."
IL: stelem.ref
IL: dup
IL: ldc.i4.2
IL: ldstr "Сколько еды этого мода на картах и в контейнерах. На картах шанс не поднимается выше 100%, поэтому 200% дают немного меньше двойного количества."
IL: stelem.ref
IL: dup
IL: ldc.i4.3
IL: ldstr "How much of this mod's food on maps and in containers. On maps a chance stops at 100%, so 200% gives a little less than double."
IL: stelem.ref
IL: dup
IL: ldc.i4.4
IL: ldstr "Сколько еды Food Pack by EMPEROR на картах и в контейнерах. Саке и карточки рецептов не меняются."
IL: stelem.ref
IL: dup
IL: ldc.i4.5
IL: ldstr "How much Food Pack by EMPEROR food on maps and in containers. Sake and recipe cards don't change."
IL: stelem.ref
IL: dup
IL: ldc.i4.6
IL: ldstr "Сколько игровой еды в контейнерах: шкафах, рюкзаках, машинах. Вода не меняется."
IL: stelem.ref
IL: dup
IL: ldc.i4.7
IL: ldstr "How much of the game's food in containers: cupboards, backpacks, cars. Water doesn't change."
IL: stelem.ref
IL: dup
IL: ldc.i4.8
IL: ldstr "Сколько игровой еды лежит на картах. Только уменьшение: при первом посещении места часть еды убирается."
IL: stelem.ref
IL: dup
IL: ldc.i4.s 9
IL: ldstr "How much of the game's food lies on maps. Only less: part of it is removed on a place's first visit."
IL: stelem.ref
IL: stloc.1
IL: call string ['Assembly-CSharp']Il2Cpp.Localization::get_Language()
IL: ldstr "Russian"
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: stloc.2
IL: ldc.i4.0
IL: stloc.3
IL: br.s IL
IL: ldloc.0
IL: ldloc.1
IL: ldloc.3
IL: ldelem.ref
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brtrue.s IL
IL: ldloc.0
IL: ldloc.1
IL: ldloc.3
IL: ldc.i4.1
IL: add
IL: ldelem.ref
IL: call bool [System.Runtime]System.String::op_Equality(string, string)
IL: brfalse.s IL
IL: ldloc.2
IL: brtrue.s IL
IL: ldloc.1
IL: ldloc.3
IL: ldc.i4.1
IL: add
IL: ldelem.ref
IL: br.s IL
IL: ldloc.1
IL: ldloc.3
IL: ldelem.ref
IL: stloc.s 4
IL: ldloc.s 4
IL: ldloc.0
IL: call bool [System.Runtime]System.String::op_Inequality(string, string)
IL: brfalse.s IL
IL: ldarg.0
IL: ldloc.s 4
IL: callvirt instance void ['Assembly-CSharp']Il2Cpp.UILabel::set_text(string)
IL: ret
IL: ldloc.3
IL: ldc.i4.2
IL: add
IL: stloc.3
IL: ldloc.3
IL: ldloc.1
IL: ldlen
IL: conv.i4
IL: blt.s IL
IL: ret
} 
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.HintLanguageOptions
extends [System.Runtime]System.Object
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor() = (
01 00 00 00
)
.method private hidebysig static 
bool Prepare () cil managed 
{
.maxstack 8
IL: ldstr "ModSettings.ModSettingsGUI:UpdateDescriptionLabel"
IL: ldnull
IL: ldnull
IL: call class [System.Runtime]System.Reflection.MethodInfo ['0Harmony']HarmonyLib.AccessTools::Method(string, class [System.Runtime]System.Type[], class [System.Runtime]System.Type[])
IL: ldnull
IL: call bool [System.Runtime]System.Reflection.MethodInfo::op_Inequality(class [System.Runtime]System.Reflection.MethodInfo, class [System.Runtime]System.Reflection.MethodInfo)
IL: ret
} 
.method private hidebysig static 
class [System.Runtime]System.Reflection.MethodBase TargetMethod () cil managed 
{
.maxstack 8
IL: ldstr "ModSettings.ModSettingsGUI:UpdateDescriptionLabel"
IL: ldnull
IL: ldnull
IL: call class [System.Runtime]System.Reflection.MethodInfo ['0Harmony']HarmonyLib.AccessTools::Method(string, class [System.Runtime]System.Type[], class [System.Runtime]System.Type[])
IL: ret
} 
.method private hidebysig static 
void Postfix () cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPriority::.ctor(int32) = (
01 00 00 00 00 00 00 00
)
.maxstack 2
.locals init (
[0] class ['Assembly-CSharp']Il2Cpp.Panel_OptionsMenu
)
IL: call !!0 ['Assembly-CSharp']Il2Cpp.InterfaceManager::GetPanel<class ['Assembly-CSharp']Il2Cpp.Panel_OptionsMenu>()
IL: stloc.0
IL: ldloc.0
IL: ldnull
IL: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
IL: brfalse.s IL
IL: ldloc.0
IL: callvirt instance class ['Assembly-CSharp']Il2Cpp.UILabel ['Assembly-CSharp']Il2Cpp.Panel_OptionsMenu::get_m_OptionDescriptionLabel()
IL: call void FoodBalanceByEMPEROR.HintLanguage::Fix(class ['Assembly-CSharp']Il2Cpp.UILabel)
IL: ret
} 
} 
.class private auto ansi abstract sealed beforefieldinit FoodBalanceByEMPEROR.HintLanguageCustomMode
extends [System.Runtime]System.Object
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPatch::.ctor(class [System.Runtime]System.Type, string) = (
01 00 62 49 6c 32 43 70 70 2e 50 61 6e 65 6c 5f
43 75 73 74 6f 6d 58 50 53 65 74 75 70 2c 20 41
73 73 65 6d 62 6c 79 2d 43 53 68 61 72 70 2c 20
56 65 72 73 69 6f 6e 3d 30 2e 30 2e 30 2e 30 2c
20 43 75 6c 74 75 72 65 3d 6e 65 75 74 72 61 6c
2c 20 50 75 62 6c 69 63 4b 65 79 54 6f 6b 65 6e
3d 6e 75 6c 6c 14 55 70 64 61 74 65 4d 65 6e 75
4e 61 76 69 67 61 74 69 6f 6e 00 00
)
.method private hidebysig static 
void Postfix (
class ['Assembly-CSharp']Il2Cpp.Panel_CustomXPSetup __instance
) cil managed 
{
.custom instance void ['0Harmony']HarmonyLib.HarmonyPriority::.ctor(int32) = (
01 00 00 00 00 00 00 00
)
.maxstack 8
IL: ldarg.0
IL: callvirt instance class ['Assembly-CSharp']Il2Cpp.UILabel ['Assembly-CSharp']Il2Cpp.Panel_CustomXPSetup::get_m_TooltipLabel()
IL: call void FoodBalanceByEMPEROR.HintLanguage::Fix(class ['Assembly-CSharp']Il2Cpp.UILabel)
IL: ret
} 
} 
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '8.2.0.7535-95108c96')
