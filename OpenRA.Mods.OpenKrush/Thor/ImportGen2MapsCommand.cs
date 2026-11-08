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

namespace OpenRA.Mods.OpenKrush.Thor;

using JetBrains.Annotations;

[UsedImplicitly]
public class ImportGen2MapsCommand : IUtilityCommand
{
	string IUtilityCommand.Name => "--import-gen2-maps";

	bool IUtilityCommand.ValidateArguments(string[] args) => args.Length == 3;

	[Desc("CONTENTDIR OUTDIR", "Import the original KKND2 multiplayer levels as maps.")]
	void IUtilityCommand.Run(Utility utility, string[] args)
	{
		Console.WriteLine($"{Gen2MapImporter.ImportAll(args[1], args[2], Console.WriteLine)} maps imported.");
	}
}
