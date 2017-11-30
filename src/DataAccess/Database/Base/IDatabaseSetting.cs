﻿namespace DataAccess.Database.Base
{
    using Config;

    public interface IDatabaseSetting
    {
        DatabaseType GetDatabaseType();
        bool IsUseReplicaSet();
        bool IsUseSharding();
        bool IsAuth();
        string GetAddress();
        string GetDatabaseName();
        string GetUserName();
        string GetPassword();
        string GetConnectionString();
        string GetReplicaSetName();
    }
}
