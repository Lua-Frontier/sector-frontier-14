using Content.Server._Orion.EnergyDome.Systems;

namespace Content.Server._Chaos.EnergyDome.Components;

//
// License-Identifier: AGPL-3.0-or-later
//

/// <summary>
/// компонент для пояса интек с повышенной прочностью и возможностью восстановления
/// </summary>
[RegisterComponent, Access(typeof(EnergyDomeSystem))]
public sealed partial class InteQShieldComponent : Component
{
    /// <summary>
    /// текущий уровень здоровья щита
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int CurrentHealth = 250;

    /// <summary>
    /// макс.здоровье щита
    /// </summary>
    [DataField]
    public int MaxHealth = 250;

    /// <summary>
    /// мин.здоровье для активации щита
    /// </summary>
    [DataField]
    public int MinHealthToActivate = 10;

    /// <summary>
    /// кд после поломки щита в секундах
    /// </summary>
    [DataField]
    public int CooldownTime = 30;

    /// <summary>
    /// время, когда щит можно будет активировать снова
    /// </summary>
    [DataField]
    public TimeSpan CooldownEndTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// находится ли щит на кд
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool OnCooldown = false;

    /// <summary>
    /// время отключения щита от любого ЭМИ
    /// </summary>
    [DataField]
    public int EmpDisableTime = 120;

    /// <summary>
    /// время, когда закончится действие ЭМИ
    /// </summary>
    [DataField]
    public TimeSpan EmpEndTime = TimeSpan.FromSeconds(120);

    /// <summary>
    /// находится ли щит под действием ЭМИ
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool EmpDisabled = false;

    /// <summary>
    /// энергия, требуемая для восстановления 1 единицы здоровья щита
    /// </summary>
    [DataField]
    public int RegenEnergyCost = 4; // будет тратиться 4 энергии, для восстановления 1 единицы прочности

    /// <summary>
    /// скорость восстановления щита в единицах здоровья в секунду
    /// </summary>
    [DataField]
    public float RegenRate = 250f / 30f; // 250 единиц здоровья за 30 секунд, чтобы полностью восстановить щит за время кд

    /// <summary>
    /// количество здоровья, восстановленное во время кд, для постепенного восстановления щита
    /// </summary>
    [DataField]
    public float AccumulatedRegen;

    /// <summary>
    /// время последнего повреждения щита
    /// </summary>
    [DataField]
    public TimeSpan? LastDamageTime;

    /// <summary>
    /// строка, которая отвечает за тип урона, который будет умножен в системке на 3
    /// </summary>
    [DataField]
    public string MultiplyDamage = "Ion";
}