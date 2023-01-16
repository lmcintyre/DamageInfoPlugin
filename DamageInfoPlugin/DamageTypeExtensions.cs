using System;

namespace DamageInfoPlugin;

public static class DamageTypeExtensions
{
	public static DamageType ToDamageType(this AttackType type)
	{
		return type switch
		{
			AttackType.Unknown => DamageType.Unique,
			AttackType.Slashing => DamageType.Physical,
			AttackType.Piercing => DamageType.Physical,
			AttackType.Blunt => DamageType.Physical,
			AttackType.Shot => DamageType.Physical,
			AttackType.Magical => DamageType.Magical,
			AttackType.Unique => DamageType.Unique,
			AttackType.Physical => DamageType.Unique,
			AttackType.LimitBreak => DamageType.Unique,
			_ => DamageType.Unique,
		};
	}
	
	public static DamageType ToDamageType(this SeDamageType type)
	{
		return type switch
		{

			SeDamageType.None => DamageType.None,
			SeDamageType.Physical => DamageType.Physical,
			SeDamageType.Magical => DamageType.Magical,
			SeDamageType.Unique => DamageType.Unique,
			_ => DamageType.None,
		};
	}

	public static SeDamageType ToSeDamageType(this DamageType type)
	{
		return type switch
		{

			DamageType.None => SeDamageType.None,
			DamageType.Physical => SeDamageType.Physical,
			DamageType.Magical => SeDamageType.Magical,
			DamageType.Unique => SeDamageType.Unique,
			_ => SeDamageType.None,
		};
	}

	public static SeDamageType ToSeDamageType(this AttackType type)
	{
		return type.ToDamageType().ToSeDamageType();
	}
}