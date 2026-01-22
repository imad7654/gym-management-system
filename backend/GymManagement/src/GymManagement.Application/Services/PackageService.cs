using GymManagement.Application.DTOs.Package;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IPackageService
{
    Task<List<PackageListDto>> GetAllPackagesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<List<PackageListDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
    Task<PackageDto?> GetPackageByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PackageDto> CreatePackageAsync(CreatePackageRequest request, CancellationToken cancellationToken = default);
    Task<PackageDto?> UpdatePackageAsync(int id, UpdatePackageRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePackageAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> TogglePackageStatusAsync(int id, CancellationToken cancellationToken = default);
}

public class PackageService : IPackageService
{
    private readonly IUnitOfWork _unitOfWork;

    public PackageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PackageListDto>> GetAllPackagesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = includeInactive
            ? _unitOfWork.Packages.QueryIncludingDeleted()
            : _unitOfWork.Packages.Query();

        return await query
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new PackageListDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                DurationDays = p.DurationDays,
                Price = p.Price,
                IsActive = p.IsActive,
                DisplayOrder = p.DisplayOrder
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PackageListDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Packages.Query()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new PackageListDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                DurationDays = p.DurationDays,
                Price = p.Price,
                IsActive = p.IsActive,
                DisplayOrder = p.DisplayOrder
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PackageDto?> GetPackageByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.Packages.QueryIncludingDeleted()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (package == null) return null;

        return MapToDto(package);
    }

    public async Task<PackageDto> CreatePackageAsync(CreatePackageRequest request, CancellationToken cancellationToken = default)
    {
        var package = new Package
        {
            Name = request.Name,
            Description = request.Description,
            DurationDays = request.DurationDays,
            Price = request.Price,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };

        await _unitOfWork.Packages.AddAsync(package, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(package);
    }

    public async Task<PackageDto?> UpdatePackageAsync(int id, UpdatePackageRequest request, CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.Packages.QueryIncludingDeleted()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (package == null) return null;

        package.Name = request.Name;
        package.Description = request.Description;
        package.DurationDays = request.DurationDays;
        package.Price = request.Price;
        package.IsActive = request.IsActive;
        package.DisplayOrder = request.DisplayOrder;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(package);
    }

    public async Task<bool> DeletePackageAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.Packages.GetByIdAsync(id, cancellationToken);
        if (package == null) return false;

        package.IsActive = false;
        package.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TogglePackageStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.Packages.QueryIncludingDeleted()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (package == null) return false;

        package.IsActive = !package.IsActive;
        if (package.IsActive)
        {
            package.DeletedAt = null;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static PackageDto MapToDto(Package package)
    {
        return new PackageDto
        {
            Id = package.Id,
            Name = package.Name,
            Description = package.Description,
            DurationDays = package.DurationDays,
            Price = package.Price,
            IsActive = package.IsActive,
            DisplayOrder = package.DisplayOrder,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }
}
