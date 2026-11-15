using System;

namespace ThinkOnErp.Application.Services.Hr;

internal static class HrApprovalGuard
{
    public static void EnsureNotSelfApproved(string approver, string? creator, string? employeeUserName, string requestType)
    {
        if (IsSameActor(approver, creator) || IsSameActor(approver, employeeUserName))
            throw new InvalidOperationException($"Self-approval is not allowed for this {requestType}.");
    }

    private static bool IsSameActor(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
