#region Copyright & License Information

/*
 * Copyright 2007-2026 The OpenKrush Developers (see AUTHORS)
 * This file is part of OpenKrush, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

namespace OpenRA.Mods.OpenKrush.Mechanics.Misc.Traits;

using JetBrains.Annotations;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Runs to the target and kills itself there (the Explodes trait deals the damage). Needs an Armament only as a valid-weapon placeholder.")]
public class AttackSuicidesInfo : AttackBaseInfo
{
	[Desc("Distance at which the actor blows up.")]
	public readonly WDist DetonationRange = new(768);

	public override object Create(ActorInitializer init)
	{
		return new AttackSuicides(init.Self, this);
	}
}

public class AttackSuicides : AttackBase
{
	public new readonly AttackSuicidesInfo Info;

	public AttackSuicides(Actor self, AttackSuicidesInfo info)
		: base(self, info)
	{
		this.Info = info;
	}

	protected override bool CanAttack(Actor self, in Target target)
	{
		return self.IsInWorld && !this.IsTraitDisabled && target.IsValidFor(self);
	}

	public override Activity GetAttackActivity(Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack, Color? targetLineColor = null)
	{
		return new SuicideActivity(self, newTarget, this.Info.DetonationRange, targetLineColor);
	}

	private sealed class SuicideActivity : Activity
	{
		private readonly IMove move;
		private readonly Target target;
		private readonly WDist range;
		private readonly Color? lineColor;

		public SuicideActivity(Actor self, in Target target, WDist range, Color? lineColor)
		{
			this.move = self.Trait<IMove>();
			this.target = target;
			this.range = range;
			this.lineColor = lineColor;
		}

		public override bool Tick(Actor self)
		{
			if (this.IsCanceling || !this.target.IsValidFor(self))
				return true;

			if ((this.target.CenterPosition - self.CenterPosition).HorizontalLengthSquared <= this.range.LengthSquared)
			{
				self.Kill(self);

				return true;
			}

			this.QueueChild(this.move.MoveWithinRange(this.target, this.range, null, this.lineColor));

			return false;
		}
	}
}
