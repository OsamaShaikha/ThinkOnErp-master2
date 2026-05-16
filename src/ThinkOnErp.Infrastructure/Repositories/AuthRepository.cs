using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly ThinkOnErpDbContext _context;

    public AuthRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<SysUser?> AuthenticateAsync(string userName, string passwordHash)
    {
        return await _context.SysUsers
            .FirstOrDefaultAsync(u => u.UserName == userName && u.Password == passwordHash && u.IsActive);
    }

    public async Task SaveRefreshTokenAsync(long userId, string refreshToken, DateTime expiryDate)
    {
        var user = await _context.SysUsers.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = expiryDate;
            user.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<SysUser?> ValidateRefreshTokenAsync(string refreshToken)
    {
        return await _context.SysUsers
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken 
                && u.RefreshTokenExpiry > DateTime.Now 
                && u.IsActive);
    }
}