using System;
using System.Collections.Generic;
using Game.Tools;
using Unity.Entities;
namespace CS2AreaSystem.Core.Prefabs
{
    // Native errors remain authoritative except for explicitly permitted managed pairs.
    public static class ManagedOverlapValidation
    {
        public static IReadOnlyCollection<Entity> FindOverlapOnlyEntities(IEnumerable<ErrorData> errors,
            Func<Entity, bool> owned, Func<Entity, Entity, bool> permits = null)
        {
            var permitted = new HashSet<Entity>();
            var blocked = new HashSet<Entity>();
            foreach (var error in errors)
            {
                if (error.m_ErrorSeverity < ErrorSeverity.Error) continue;
                bool allowed = error.m_ErrorSeverity == ErrorSeverity.Error && error.m_ErrorType == ErrorType.OverlapExisting &&
                    error.m_TempEntity != Entity.Null && error.m_PermanentEntity != Entity.Null &&
                    owned(error.m_TempEntity) && owned(error.m_PermanentEntity) &&
                    (permits == null || permits(error.m_TempEntity, error.m_PermanentEntity));
                var destination = allowed ? permitted : blocked;
                if (error.m_TempEntity != Entity.Null) destination.Add(error.m_TempEntity);
                if (error.m_PermanentEntity != Entity.Null) destination.Add(error.m_PermanentEntity);
            }
            permitted.ExceptWith(blocked);
            return permitted;
        }
    }
}
