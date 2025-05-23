// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Necromorphs.Unitology;

public sealed partial class TileSpawnActionEvent : InstantActionEvent
{

}

public sealed partial class ObeliskActionEvent : InstantActionEvent
{

}

public sealed partial class UnitologyHeadActionEvent : EntityTargetActionEvent
{

}

public sealed partial class OrderToSlaveActionEvent : EntityTargetActionEvent
{

}

public sealed partial class SelectTargetRecruitmentEvent : EntityTargetActionEvent
{

}

[Serializable, NetSerializable]
public sealed partial class ObeliskActivateDoAfterEvent : SimpleDoAfterEvent
{

}

[Serializable, NetSerializable]
public sealed partial class UnitologistRecruitmentDoAfterEvent : SimpleDoAfterEvent
{

}
