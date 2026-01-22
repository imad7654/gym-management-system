using GymManagement.Application.DTOs.GymInfo;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IGymInfoService
{
    Task<GymInfoDto?> GetGymInfoAsync(CancellationToken cancellationToken = default);
    Task<GymInfoDto?> UpdateGymInfoAsync(UpdateGymInfoRequest request, int? userId = null, CancellationToken cancellationToken = default);
}

public class GymInfoService : IGymInfoService
{
    private readonly IUnitOfWork _unitOfWork;

    public GymInfoService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GymInfoDto?> GetGymInfoAsync(CancellationToken cancellationToken = default)
    {
        var gymInfo = await _unitOfWork.GymInfos.Query()
            .FirstOrDefaultAsync(cancellationToken);

        if (gymInfo == null) return null;

        return new GymInfoDto
        {
            Id = gymInfo.Id,
            GymName = gymInfo.GymName,
            LogoUrl = gymInfo.LogoUrl,
            Description = gymInfo.Description,
            Address = gymInfo.Address,
            PhoneNumber = gymInfo.PhoneNumber,
            Email = gymInfo.Email,
            FacebookUrl = gymInfo.FacebookUrl,
            InstagramUrl = gymInfo.InstagramUrl,
            TwitterUrl = gymInfo.TwitterUrl,
            OperatingHours = gymInfo.OperatingHours,
            HeroTitle = gymInfo.HeroTitle,
            HeroSubtitle = gymInfo.HeroSubtitle,
            HeroImageUrl = gymInfo.HeroImageUrl,
            AboutTitle = gymInfo.AboutTitle,
            AboutContent = gymInfo.AboutContent,
            MetaTitle = gymInfo.MetaTitle,
            MetaDescription = gymInfo.MetaDescription
        };
    }

    public async Task<GymInfoDto?> UpdateGymInfoAsync(UpdateGymInfoRequest request, int? userId = null, CancellationToken cancellationToken = default)
    {
        var gymInfo = await _unitOfWork.GymInfos.Query()
            .FirstOrDefaultAsync(cancellationToken);

        if (gymInfo == null) return null;

        gymInfo.GymName = request.GymName;
        gymInfo.LogoUrl = request.LogoUrl;
        gymInfo.Description = request.Description;
        gymInfo.Address = request.Address;
        gymInfo.PhoneNumber = request.PhoneNumber;
        gymInfo.Email = request.Email;
        gymInfo.FacebookUrl = request.FacebookUrl;
        gymInfo.InstagramUrl = request.InstagramUrl;
        gymInfo.TwitterUrl = request.TwitterUrl;
        gymInfo.OperatingHours = request.OperatingHours;
        gymInfo.HeroTitle = request.HeroTitle;
        gymInfo.HeroSubtitle = request.HeroSubtitle;
        gymInfo.HeroImageUrl = request.HeroImageUrl;
        gymInfo.AboutTitle = request.AboutTitle;
        gymInfo.AboutContent = request.AboutContent;
        gymInfo.MetaTitle = request.MetaTitle;
        gymInfo.MetaDescription = request.MetaDescription;
        gymInfo.UpdatedBy = userId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GymInfoDto
        {
            Id = gymInfo.Id,
            GymName = gymInfo.GymName,
            LogoUrl = gymInfo.LogoUrl,
            Description = gymInfo.Description,
            Address = gymInfo.Address,
            PhoneNumber = gymInfo.PhoneNumber,
            Email = gymInfo.Email,
            FacebookUrl = gymInfo.FacebookUrl,
            InstagramUrl = gymInfo.InstagramUrl,
            TwitterUrl = gymInfo.TwitterUrl,
            OperatingHours = gymInfo.OperatingHours,
            HeroTitle = gymInfo.HeroTitle,
            HeroSubtitle = gymInfo.HeroSubtitle,
            HeroImageUrl = gymInfo.HeroImageUrl,
            AboutTitle = gymInfo.AboutTitle,
            AboutContent = gymInfo.AboutContent,
            MetaTitle = gymInfo.MetaTitle,
            MetaDescription = gymInfo.MetaDescription
        };
    }
}
