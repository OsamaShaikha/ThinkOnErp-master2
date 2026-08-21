using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ThinkOnErp.API.Authorization;

namespace ThinkOnErp.API.Swagger;

/// <summary>
/// Enriches every Swagger API operation with visual badges and descriptive markdown banners
/// indicating whether the endpoint belongs to SuperAdmin, Tenant Company, Public, or System scope.
/// </summary>
public sealed class SwaggerScopeBadgeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var controllerType = method.DeclaringType;
        var controllerName = controllerType?.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase) ?? string.Empty;
        var routeTemplate = context.ApiDescription.RelativePath ?? string.Empty;
        var httpMethod = context.ApiDescription.HttpMethod ?? string.Empty;

        // 1. Detect Scope
        var scope = DetermineScope(method, controllerType, controllerName, routeTemplate);

        // 2. Format Badge and Title
        var badge = scope switch
        {
            EndpointScope.SuperAdmin => "🛡️ [SuperAdmin]",
            EndpointScope.TenantCompany => "🏢 [Tenant]",
            EndpointScope.Public => "🌐 [Public]",
            EndpointScope.System => "⚙️ [System]",
            _ => "🏢 [Tenant]"
        };

        // Update Summary with Badge
        var originalSummary = operation.Summary;
        if (string.IsNullOrWhiteSpace(originalSummary))
        {
            originalSummary = $"{httpMethod} {controllerName}";
        }

        // Avoid duplicate badges if already applied
        if (!originalSummary.Contains("[SuperAdmin]") &&
            !originalSummary.Contains("[Tenant]") &&
            !originalSummary.Contains("[Public]") &&
            !originalSummary.Contains("[System]"))
        {
            operation.Summary = $"{badge} {originalSummary}";
        }

        // 3. Prepend Markdown Scope Banner to Description
        var banner = GenerateScopeBanner(scope);
        if (string.IsNullOrWhiteSpace(operation.Description))
        {
            operation.Description = banner;
        }
        else if (!operation.Description.Contains("**Scope**"))
        {
            operation.Description = $"{banner}\n\n{operation.Description}";
        }
    }

    private static EndpointScope DetermineScope(
        MethodInfo method,
        Type? controllerType,
        string controllerName,
        string routeTemplate)
    {
        // Check for AllowAnonymous (Public)
        var hasAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null ||
                           (controllerType?.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null);

        if (hasAnonymous && (routeTemplate.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                             routeTemplate.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                             routeTemplate.Contains("public", StringComparison.OrdinalIgnoreCase) ||
                             routeTemplate.Contains("health", StringComparison.OrdinalIgnoreCase)))
        {
            return EndpointScope.Public;
        }

        // Check for SuperAdmin
        var authAttributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(controllerType?.GetCustomAttributes<AuthorizeAttribute>(inherit: true) ?? Enumerable.Empty<AuthorizeAttribute>())
            .ToList();

        var isSuperAdmin = authAttributes.Any(a =>
                               string.Equals(a.Roles, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(a.Policy, "SuperAdminOnly", StringComparison.OrdinalIgnoreCase)) ||
                           controllerName.StartsWith("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                           routeTemplate.StartsWith("api/superadmin", StringComparison.OrdinalIgnoreCase) ||
                           routeTemplate.StartsWith("api/superadmins", StringComparison.OrdinalIgnoreCase);

        if (isSuperAdmin)
        {
            return EndpointScope.SuperAdmin;
        }

        // Check for Tenant Scoped
        var hasTenantScoped = method.GetCustomAttribute<TenantScopedAttribute>(inherit: true) != null ||
                              (controllerType?.GetCustomAttribute<TenantScopedAttribute>(inherit: true) != null);

        if (hasTenantScoped ||
            routeTemplate.StartsWith("api/accounting", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/companies", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/branches", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/customers", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/vendors", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/users", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/roles", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/permissions", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/subledger", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/pdc", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/banking", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/tickets", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/audit", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointScope.TenantCompany;
        }

        // System settings / shared codes
        if (routeTemplate.StartsWith("api/syscodes", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/sys-settings", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/modules", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/screens", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/features", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/configuration", StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.StartsWith("api/documents", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointScope.System;
        }

        return EndpointScope.TenantCompany;
    }

    private static string GenerateScopeBanner(EndpointScope scope)
    {
        return scope switch
        {
            EndpointScope.SuperAdmin =>
                "> 🛡️ **Scope**: SUPER_ADMIN_ONLY (إدارة المنصة الشاملة)\n" +
                "> **الصلاحية المطلوبة**: يتطلب حساب مسؤول النظام العام (SuperAdmin JWT Token).\n" +
                "> **نطاق التنفيذ**: ينفذ على مستوى النظام العام أو إدارة الشركات والقوالب (DEV_TEMPLATE).",

            EndpointScope.TenantCompany =>
                "> 🏢 **Scope**: TENANT_COMPANY (خاص بالشركات والمستأجرين)\n" +
                "> **الصلاحية المطلوبة**: مستخدم شركة مرخص (Company User JWT) أو SuperAdmin مع اختيار الشركة.\n" +
                "> **عزل البيانات**: تنفذ العمليات تلقائياً داخل الـ Oracle Schema الخاصة بالشركة المستهدفة (مثال: THINKONERP_1122).\n" +
                "> **هيدرات اختيار الشركة (لـ SuperAdmin)**: X-Company-Code أو X-Company-Id.",

            EndpointScope.Public =>
                "> 🌐 **Scope**: PUBLIC / AUTH (عام / مصادقة)\n" +
                "> **الصلاحية المطلوبة**: لا يتطلب تسجيل دخول مسبق (Anonymous / Pre-Authentication).",

            EndpointScope.System =>
                "> ⚙️ **Scope**: SYSTEM_SHARED (إعدادات ورموز النظام المشتركة)\n" +
                "> **الصلاحية المطلوبة**: مستخدم مسجل في النظام مع صلاحيات القراءة أو التهيئة.",

            _ => string.Empty
        };
    }

    private enum EndpointScope
    {
        SuperAdmin,
        TenantCompany,
        Public,
        System
    }
}
