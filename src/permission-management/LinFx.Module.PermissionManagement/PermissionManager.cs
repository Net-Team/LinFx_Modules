﻿using LinFx.Extensions.Authorization.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LinFx.Extensions.PermissionManagement
{
    /// <summary>
    /// 权限管理器
    /// </summary>
    [Service(ServiceLifetime.Singleton)]
    public class PermissionManager
    {
        //protected IPermissionGrantRepository PermissionGrantRepository { get; }

        private readonly Lazy<List<IPermissionManagementProvider>> _lazyProviders;

        protected IPermissionDefinitionManager PermissionDefinitionManager { get; }

        //protected ICurrentTenant CurrentTenant { get; }

        protected IReadOnlyList<IPermissionManagementProvider> ManagementProviders => _lazyProviders.Value;


        protected PermissionManagementOptions Options { get; }

        public PermissionManager(
            IPermissionDefinitionManager permissionDefinitionManager,
            //IPermissionGrantRepository permissionGrantRepository,
            IServiceProvider serviceProvider,
            IOptions<PermissionManagementOptions> options
            //ICurrentTenant currentTenant
            )
        {
            //CurrentTenant = currentTenant;
            //PermissionGrantRepository = permissionGrantRepository;
            PermissionDefinitionManager = permissionDefinitionManager;
            Options = options.Value;

            _lazyProviders = new Lazy<List<IPermissionManagementProvider>>(() => Options
                    .ManagementProviders
                    .Select(c => serviceProvider.GetRequiredService(c) as IPermissionManagementProvider)
                    .ToList(), true);
        }

        public async Task<PermissionWithGrantedProviders> GetAsync(string permissionName, string providerName, string providerKey)
        {
            return await GetInternalAsync(PermissionDefinitionManager.Get(permissionName), providerName, providerKey);
        }

        //public async Task SetAsync(string permissionName, string providerName, string providerKey, bool isGranted)
        //{
        //    var permission = PermissionDefinitionManager.Get(permissionName);

        //    //if (permission.Providers.Any() && !permission.Providers.Contains(providerName))
        //    //{
        //    //    //TODO: BusinessException
        //    //    throw new ApplicationException($"The permission named '{permission.Name}' has not compatible with the provider named '{providerName}'");
        //    //}

        //    //if (!permission.MultiTenancySide.HasFlag(CurrentTenant.GetMultiTenancySide()))
        //    //{
        //    //    //TODO: BusinessException
        //    //    throw new ApplicationException($"The permission named '{permission.Name}' has multitenancy side '{permission.MultiTenancySide}' which is not compatible with the current multitenancy side '{CurrentTenant.GetMultiTenancySide()}'");
        //    //}

        //    var currentGrantInfo = await GetInternalAsync(permission, providerName, providerKey);
        //    if (currentGrantInfo.IsGranted == isGranted)
        //    {
        //        return;
        //    }

        //    var provider = ManagementProviders.FirstOrDefault(m => m.Name == providerName);
        //    if (provider == null)
        //    {
        //        //TODO: BusinessException
        //        throw new AbpException("Unknown permission management provider: " + providerName);
        //    }

        //    await provider.SetAsync(permissionName, providerKey, isGranted);
        //}

        protected virtual async Task<PermissionWithGrantedProviders> GetInternalAsync(PermissionDefinition permission, string providerName, string providerKey)
        {
            var result = new PermissionWithGrantedProviders(permission.Name, false);

            //if (!permission.MultiTenancySide.HasFlag(CurrentTenant.GetMultiTenancySide()))
            //{
            //    return result;
            //}

            if (permission.Providers.Any() && !permission.Providers.Contains(providerName))
            {
                return result;
            }

            foreach (var provider in ManagementProviders)
            {
                var providerResult = await provider.CheckAsync(permission.Name, providerName, providerKey);
                if (providerResult.IsGranted)
                {
                    result.IsGranted = true;
                    result.Providers.Add(new PermissionValueProviderInfo(provider.Name, providerResult.ProviderKey));
                }
            }

            return result;
        }
    }
}
