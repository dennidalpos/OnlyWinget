// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public interface IWingetPackageInterrogationService
{
    Task<PackageInterrogationResult> InterrogateAsync(PackageInterrogationRequest request);
}
