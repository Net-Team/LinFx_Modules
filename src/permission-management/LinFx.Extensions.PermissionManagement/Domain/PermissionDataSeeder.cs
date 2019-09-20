﻿using LinFx.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LinFx.Extensions.PermissionManagement.Domain
{
    public class PermissionDataSeeder
    {
        protected IPermissionGrantRepository PermissionGrantRepository { get; }

        public PermissionDataSeeder(IPermissionGrantRepository permissionGrantRepository)
        {
            PermissionGrantRepository = permissionGrantRepository;
        }

        public async Task SeedAsync(
            string providerName,
            string providerKey,
            IEnumerable<string> grantedPermissions,
            string tenantId = default)
        {
            foreach (var permissionName in grantedPermissions)
            {
                if (await PermissionGrantRepository.FindAsync(permissionName, providerName, providerKey) != null)
                {
                    continue;
                }

                await PermissionGrantRepository.InsertAsync(
                    new PermissionGrant(
                        IDUtils.NewId().ToString(),
                        permissionName,
                        providerName,
                        providerKey,
                        tenantId
                    )
                );
            }
        }
    }
}