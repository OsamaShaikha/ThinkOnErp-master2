# Infrastructure Services

This directory contains service implementations for the Infrastructure layer.

## PasswordHashingService

### Overview
The `PasswordHashingService` provides SHA-256 password hashing functionality for secure password storage and authentication.

### Features
- Hashes passwords using SHA-256 algorithm
- Converts hash to uppercase hexadecimal string representation (64 characters)
- Validates input to prevent null or empty passwords
- Deterministic hashing (same password always produces same hash)

### Usage

#### In User Creation (Application Layer)
```csharp
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, decimal>
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHashingService _passwordHashingService;

    public async Task<decimal> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Hash the password before storing
        var hashedPassword = _passwordHashingService.HashPassword(request.Password);
        
        var user = new SysUser
        {
            UserName = request.UserName,
            Password = hashedPassword, // Store hashed password
            // ... other properties
        };
        
        return await _userRepository.CreateAsync(user);
    }
}
```

#### In Authentication (Application Layer)
```csharp
public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenDto>
{
    private readonly IAuthRepository _authRepository;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly JwtTokenService _jwtTokenService;

    public async Task<TokenDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Hash the provided password before comparison
        var hashedPassword = _passwordHashingService.HashPassword(request.Password);
        
        // Authenticate with hashed password
        var user = await _authRepository.AuthenticateAsync(request.UserName, hashedPassword);
        
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }
        
        return _jwtTokenService.GenerateToken(user);
    }
}
```

#### In Password Change (Application Layer)
```csharp
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHashingService _passwordHashingService;

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        
        // Hash the new password
        user.Password = _passwordHashingService.HashPassword(request.NewPassword);
        
        await _userRepository.UpdateAsync(user);
        
        return Unit.Value;
    }
}
```

### Security Considerations

1. **SHA-256 Algorithm**: Uses industry-standard SHA-256 hashing algorithm
2. **Hexadecimal Encoding**: Produces 64-character uppercase hexadecimal string
3. **No Plain Text Storage**: Passwords are never stored in plain text
4. **Deterministic**: Same password always produces same hash (required for authentication)

### Requirements Satisfied

- **Requirement 3.1**: Passwords are hashed using SHA-256 and stored as hexadecimal strings
- **Requirement 3.2**: Passwords are hashed before authentication comparison
- **Requirement 3.3**: Password comparison uses SHA-256 hexadecimal hash values
- **Requirement 3.4**: Plain text passwords are never stored

### Testing

The service includes comprehensive unit tests and integration tests:
- Unit tests verify hashing behavior, edge cases, and error handling
- Integration tests demonstrate real-world usage scenarios
- All tests can be run with: `dotnet test --filter "FullyQualifiedName~PasswordHashing"`

### Example Hash Output

```
Input:  "test"
Output: "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08"
```

### Dependency Injection

The service is registered in the `AddInfrastructure` extension method:

```csharp
services.AddScoped<PasswordHashingService>();
```

---

## JwtTokenService

### Overview
The `JwtTokenService` generates JWT (JSON Web Token) tokens for authenticated users. It reads JWT configuration settings from appsettings.json and creates tokens with all required user claims.

### Features
- Generates JWT tokens with user claims (userId, userName, role, branchId, isAdmin)
- Signs tokens using HMAC-SHA256 algorithm
- Reads configuration from appsettings.json (SecretKey, Issuer, Audience, ExpiryInMinutes)
- Sets token expiration time based on configuration
- Returns TokenDto with access token, expiration time, and token type

### Configuration

Add the following to your `appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "YourSecretKeyMustBeAtLeast32CharactersLong",
    "Issuer": "ThinkOnErp",
    "Audience": "ThinkOnErpUsers",
    "ExpiryInMinutes": 60
  }
}
```

### Usage

#### In Login Handler (Application Layer)
```csharp
public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenDto?>
{
    private readonly IAuthRepository _authRepository;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly JwtTokenService _jwtTokenService;

    public async Task<TokenDto?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Hash the provided password
        var hashedPassword = _passwordHashingService.HashPassword(request.Password);
        
        // Authenticate user
        var user = await _authRepository.AuthenticateAsync(request.UserName, hashedPassword);
        
        if (user == null)
        {
            return null; // Invalid credentials
        }
        
        // Generate JWT token for authenticated user
        return _jwtTokenService.GenerateToken(user);
    }
}
```

### Token Claims

The generated JWT token includes the following claims:

| Claim Name | Description | Example Value |
|------------|-------------|---------------|
| `userId` | User's RowId from database | "123" |
| `userName` | User's unique username | "john.doe" |
| `role` | User's role ID (or "0" if null) | "5" |
| `branchId` | User's branch ID (or "0" if null) | "10" |
| `isAdmin` | Admin flag (lowercase boolean) | "true" or "false" |
| `iss` | Token issuer from configuration | "ThinkOnErp" |
| `aud` | Token audience from configuration | "ThinkOnErpUsers" |
| `exp` | Token expiration timestamp | Unix timestamp |

### Token Structure

Example decoded JWT token:

```json
{
  "userId": "123",
  "userName": "john.doe",
  "role": "5",
  "branchId": "10",
  "isAdmin": "false",
  "iss": "ThinkOnErp",
  "aud": "ThinkOnErpUsers",
  "exp": 1234567890
}
```

### Security Considerations

1. **HMAC-SHA256 Signing**: Tokens are signed using HMAC-SHA256 algorithm
2. **Secret Key**: Must be at least 32 characters long for security
3. **Token Expiration**: Tokens expire after configured time (default 60 minutes)
4. **Issuer/Audience Validation**: Tokens include issuer and audience for validation
5. **Claims-Based Authorization**: All user information embedded in token claims

### Requirements Satisfied

- **Requirement 2.1**: Generates JWT token with userId, userName, role, branchId, isAdmin claims
- **Requirement 2.3**: Signs tokens using configured SecretKey
- **Requirement 2.4**: Sets expiration time according to ExpiryInMinutes configuration
- **Requirement 2.5**: Includes Issuer and Audience claims from configuration

### Testing

The service includes comprehensive unit tests:
- Validates token structure and all required claims
- Verifies HMAC-SHA256 signing algorithm
- Tests issuer, audience, and expiration time
- Handles null user properties gracefully
- All tests can be run with: `dotnet test --filter "FullyQualifiedName~JwtTokenService"`

### Example Token Generation

```csharp
var user = new SysUser
{
    RowId = 123,
    UserName = "john.doe",
    Role = 5,
    BranchId = 10,
    IsAdmin = false
};

var tokenDto = _jwtTokenService.GenerateToken(user);

// tokenDto.AccessToken: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
// tokenDto.ExpiresAt: DateTime (60 minutes from now)
// tokenDto.TokenType: "Bearer"
```

### Dependency Injection

The service is registered in the `AddInfrastructure` extension method:

```csharp
services.AddScoped<JwtTokenService>();
```

### API Usage

In controllers, use the token in the Authorization header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

The API layer will validate the token and populate the security context with user claims for authorization decisions.

