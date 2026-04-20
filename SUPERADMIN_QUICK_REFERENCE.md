# SuperAdmin Quick Reference Card

## 🚀 Quick Start

### 1. Execute Seed Data
```sql
@Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql
```

### 2. Login Credentials

| Username | Password | Status |
|----------|----------|--------|
| `superadmin` | `SuperAdmin123!` | ✅ Active |
| `tech.admin` | `Admin@2024` | ✅ Active |
| `security.admin` | `SecurePass#456` | ✅ Active |
| `test.superadmin` | `SuperAdmin123!` | ❌ Inactive |

---

## 📡 API Endpoints

### Login
```http
POST /api/auth/superadmin/login
Content-Type: application/json

{
  "userName": "superadmin",
  "password": "SuperAdmin123!"
}
```

### Refresh Token
```http
POST /api/auth/superadmin/refresh
Content-Type: application/json

{
  "refreshToken": "your-refresh-token"
}
```

### Get All SuperAdmins
```http
GET /api/superadmins
Authorization: Bearer {token}
```

### Create SuperAdmin
```http
POST /api/superadmins
Authorization: Bearer {token}
Content-Type: application/json

{
  "nameAr": "اسم المدير",
  "nameEn": "Admin Name",
  "userName": "newadmin",
  "password": "SecurePass123!",
  "email": "admin@example.com",
  "phone": "+966501234567"
}
```

---

## 🔐 Password Hashes (SHA-256)

| Password | Hash |
|----------|------|
| `SuperAdmin123!` | `8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918` |
| `Admin@2024` | `5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8` |
| `SecurePass#456` | `3C9909AFEC25354D551DAE21590BB26E38D53F2173B8D3DC3EEE4C047E7AB1C1` |

---

## 🧪 Quick Test (Bash)

```bash
# Test login
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}'

# Save token
TOKEN="paste-token-here"

# Get all super admins
curl -X GET http://localhost:5000/api/superadmins \
  -H "Authorization: Bearer $TOKEN"
```

---

## 📊 Database Queries

### View All SuperAdmins
```sql
SELECT USER_NAME, ROW_DESC_E, EMAIL, 
       CASE WHEN IS_ACTIVE='1' THEN 'Active' ELSE 'Inactive' END AS STATUS
FROM SYS_SUPER_ADMIN;
```

### Activate Test Account
```sql
UPDATE SYS_SUPER_ADMIN SET IS_ACTIVE = '1' WHERE USER_NAME = 'test.superadmin';
COMMIT;
```

### Reset Password
```sql
UPDATE SYS_SUPER_ADMIN 
SET PASSWORD = 'NEW_SHA256_HASH' 
WHERE USER_NAME = 'superadmin';
COMMIT;
```

---

## ⚠️ Security Checklist

- [ ] Change default passwords
- [ ] Enable 2FA for all accounts
- [ ] Update email addresses
- [ ] Deactivate test account
- [ ] Review access logs regularly
- [ ] Use strong passwords (8+ chars, mixed case, numbers, symbols)

---

## 📁 Related Files

- `Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql` - Seed data script
- `Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql` - Login procedure
- `SUPERADMIN_SEED_DATA_CREDENTIALS.md` - Full credentials guide
- `SUPERADMIN_LOGIN_IMPLEMENTATION_COMPLETE.md` - Implementation details
- `SUPERADMIN_API_TESTING_GUIDE.md` - Complete testing guide

---

**⚠️ WARNING:** These are test credentials. Change passwords before production deployment!
