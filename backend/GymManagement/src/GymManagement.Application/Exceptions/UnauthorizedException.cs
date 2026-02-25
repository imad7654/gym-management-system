using System;

namespace GymManagement.Application.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException()
        : base("You are not authorized to perform this action.")
    {
    }
}
