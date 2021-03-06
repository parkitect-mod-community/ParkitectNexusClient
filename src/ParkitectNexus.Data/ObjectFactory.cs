﻿// ParkitectNexusClient
// Copyright (C) 2016 ParkitectNexus, Tim Potze
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using ParkitectNexus.Data.Assets;
using ParkitectNexus.Data.Assets.CachedData;
using ParkitectNexus.Data.Assets.Meta;
using ParkitectNexus.Data.Assets.Modding;
using ParkitectNexus.Data.Authentication;
using ParkitectNexus.Data.Caching;
using ParkitectNexus.Data.Game;
using ParkitectNexus.Data.Presenter;
using ParkitectNexus.Data.Reporting;
using ParkitectNexus.Data.Settings;
using ParkitectNexus.Data.Tasks;
using ParkitectNexus.Data.Updating;
using ParkitectNexus.Data.Utilities;
using ParkitectNexus.Data.Web;
using StructureMap;

namespace ParkitectNexus.Data
{
    public static class ObjectFactory
    {
        public static IContainer Container { get; private set; }

        public static Registry ConfigureStructureMap()
        {
            var registry = new Registry();

            registry.IncludeRegistry<WebRegistry>();
            registry.IncludeRegistry<GameRegistry>();
            registry.IncludeRegistry<PresenterRegistry>();
            registry.IncludeRegistry<UtilityRegistry>();

            // repository settings
            registry.For(typeof (ISettingsRepository<>)).Singleton().Use(typeof (SettingsRepository<>));

            // used to send crash reports
            registry.For<ICrashReporterFactory>().Use<CrashReporterFactory>();

            // caching
            registry.For<ICacheManager>().Use<CacheManager>();

            registry.For<IQueueableTaskManager>().Singleton().Use<QueueableTaskManager>();

            registry.For<IAuthManager>().Singleton().Use<AuthManager>();

            registry.For<ILocalAssetRepository>().Use<LocalAssetRepository>();
            registry.For<IRemoteAssetRepository>().Use<RemoteAssetRepository>();
            registry.For<IAssetMetadataStorage>().Use<AssetMetadataStorage>();
            registry.For<IAssetCachedDataStorage>().Use<AssetCachedDataStorage>();
            registry.For<IModCompiler>().Use<ModCompiler>();
            registry.For<IModLoadOrderBuilder>().Use<ModLoadOrderBuilder>();
            registry.For<IUpdateManager>().Use<UpdateManager>();

            return registry;
        }

        public static T GetInstance<T>()
        {
            return Container.GetInstance<T>();
        }

        public static object GetInstance(Type type)
        {
            return Container.GetInstance(type);
        }

        public static T GetInstance<T>(Type type)
        {
            var instance = GetInstance(type);
            return instance is T ? (T) instance : default(T);
        }

        public static ExplicitArgsExpression With<TArg>(TArg arg)
        {
            return Container.With(arg);
        }

        public static void SetUpContainer(Registry registry)
        {
            Container = new Container(registry);
        }
    }
}
