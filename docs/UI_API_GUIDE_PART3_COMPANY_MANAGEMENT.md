# ThinkOnERP - UI/API Comprehensive Guide
## Part 3: Company Management

---

## Table of Contents
1. [Company List Screen](#company-list-screen)
2. [Create Company Screen](#create-company-screen)
3. [Edit Company Screen](#edit-company-screen)
4. [Company Details Screen](#company-details-screen)
5. [Logo Management](#logo-management)

---

## Company List Screen

**Route**: `/companies`  
**Access**: Authenticated users  
**Authorization**: Authenticated policy

### API Integration
**Endpoint**: `GET /api/companies`

**Headers**:
```
Authorization: Bearer {jwt_token}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Companies retrieved successfully with logos",
  "data": [
    {
      "companyId": 1,
      "companyNameAr": "شركة الاختبار",
      "companyNameEn": "Test Company",
      "companyCode": "TC001",
      "legalNameAr": "شركة الاختبار للتجارة",
      "legalNameEn": "Test Company for Trading",
      "taxNumber": "123456789012345",
      "countryId": 1,
      "currId": 1,
      "defaultBranchId": 5,
      "companyLogoBase64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...",
      "isActive": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "branchCount": 5,
      "userCount": 45
    }
  ],
  "statusCode": 200
}
```

### UI Components

#### 1. Page Header
- **Title**: "Companies" / "الشركات"
- **Create Button**: "Create Company" (Primary button, AdminOnly)
- **Search Bar**: Search by name, code, or tax number
- **Filter Dropdown**: Filter by status (All/Active/Inactive)

#### 2. Companies Table

**Columns**:

| Column | Data Field | Type | Features |
|--------|-----------|------|----------|
| Logo | `companyLogoBase64` | Image | 40x40px thumbnail |
| Code | `companyCode` | Text | Sortable |
| Company Name | `companyNameEn` / `companyNameAr` | Text | Sortable, Language toggle |
| Legal Name | `legalNameEn` / `legalNameAr` | Text | Truncated with tooltip |
| Tax Number | `taxNumber` | Text | Formatted |
| Branches | `branchCount` | Number | Badge |
| Users | `userCount` | Number | Badge |
| Status | `isActive` | Badge | Active (green) / Inactive (red) |
| Actions | - | Buttons | View, Edit, Delete |

**Features**:
- **Sorting**: Click column headers to sort
- **Pagination**: 10/25/50/100 items per page
- **Search**: Real-time search across name, code, tax number
- **Language Toggle**: Switch between Arabic/English names
- **Row Click**: Navigate to company details
- **Logo Display**: Show company logo or placeholder icon

**Action Buttons** (Dropdown menu):
- **View Details**: Navigate to company details page
- **Edit**: Navigate to edit page (AdminOnly)
- **Delete**: Soft delete with confirmation (AdminOnly)
- **View Branches**: Navigate to branches filtered by company

#### 3. Empty State
When no companies exist:
- Icon: Building icon
- Message: "No companies found"
- Action: "Create Company" button (AdminOnly)

---

## Create Company Screen

**Route**: `/companies/create`  
**Access**: AdminOnly  
**Authorization**: AdminOnly policy

### API Integration
**Endpoint**: `POST /api/companies`

**Request Body**:
```json
{
  "companyNameAr": "شركة الاختبار",
  "companyNameEn": "Test Company",
  "companyCode": "TC001",
  "legalNameAr": "شركة الاختبار للتجارة",
  "legalNameEn": "Test Company for Trading",
  "taxNumber": "123456789012345",
  "countryId": 1,
  "currId": 1,
  "companyLogoBase64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...",
  "branchLogoBase64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...",
  "branchNameAr": "الفرع الرئيسي",
  "branchNameEn": "Main Branch",
  "branchDefaultLang": "ar",
  "branchBaseCurrencyId": 1,
  "branchRoundingRules": 2,
  "branchPhone": "+966112345678",
  "branchMobile": "+966501234567",
  "branchFax": "+966112345679",
  "branchEmail": "main@testcompany.com"
}
```

**Success Response** (201 Created):
```json
{
  "success": true,
  "message": "Company and default branch created successfully with logos",
  "data": {
    "companyId": 10,
    "branchId": 25,
    "message": "Company and default branch created successfully"
  },
  "statusCode": 201
}
```

**Validation Errors** (400 Bad Request):
```json
{
  "success": false,
  "message": "One or more validation errors occurred",
  "errors": [
    "Company code must be unique",
    "Tax number must be 15 digits",
    "Company name (English) is required"
  ],
  "statusCode": 400
}
```

### UI Components

#### Form Layout: Tabbed Interface

### Tab 1: Company Information

**Company Name (Arabic)** - Text Input
- Label: "اسم الشركة بالعربية"
- Validation: Required, max 200 characters
- Placeholder: "أدخل اسم الشركة بالعربية"

**Company Name (English)** - Text Input
- Label: "Company Name (English)"
- Validation: Required, max 200 characters
- Placeholder: "Enter company name in English"

**Company Code** - Text Input
- Label: "Company Code"
- Validation: Required, unique, max 20 characters, alphanumeric
- Placeholder: "e.g., TC001"
- Help Text: "Unique identifier for the company"

**Legal Name (Arabic)** - Text Input
- Label: "الاسم القانوني بالعربية"
- Validation: Optional, max 200 characters
- Placeholder: "أدخل الاسم القانوني"

**Legal Name (English)** - Text Input
- Label: "Legal Name (English)"
- Validation: Optional, max 200 characters
- Placeholder: "Enter legal name"

**Tax Number** - Text Input
- Label: "Tax Number"
- Validation: Required, exactly 15 digits
- Placeholder: "123456789012345"
- Input Mask: "###############"
- Help Text: "15-digit tax registration number"

**Country** - Dropdown Select
- Label: "Country"
- Validation: Required
- Options: Load from countries API
- Default: Saudi Arabia

**Base Currency** - Dropdown Select
- Label: "Base Currency"
- Validation: Required
- Options: Load from `/api/currencies`
- Default: SAR (Saudi Riyal)

**Company Logo** - File Upload
- Label: "Company Logo"
- Validation: Optional, max 2MB, formats: PNG, JPG, JPEG
- Preview: Show uploaded image
- Features:
  - Drag & drop support
  - Click to browse
  - Image preview with remove button
  - Automatic Base64 conversion
- Recommended Size: 200x200px

---

### Tab 2: Default Branch Information

**Branch Name (Arabic)** - Text Input
- Label: "اسم الفرع بالعربية"
- Validation: Optional, max 200 characters, defaults to company name
- Placeholder: "الفرع الرئيسي"
- Default Value: Same as company name

**Branch Name (English)** - Text Input
- Label: "Branch Name (English)"
- Validation: Optional, max 200 characters, defaults to company name
- Placeholder: "Main Branch"
- Default Value: Same as company name

**Default Language** - Radio Buttons
- Label: "Default Language"
- Options: Arabic (ar) / English (en)
- Default: Arabic
- Validation: Required

**Base Currency** - Dropdown Select
- Label: "Branch Base Currency"
- Validation: Required
- Options: Load from `/api/currencies`
- Default: Inherited from company

**Rounding Rules** - Number Input
- Label: "Decimal Places"
- Validation: Required, min 0, max 4
- Default: 2
- Help Text: "Number of decimal places for calculations"

**Branch Logo** - File Upload
- Label: "Branch Logo"
- Validation: Optional, max 2MB, formats: PNG, JPG, JPEG
- Preview: Show uploaded image
- Features: Same as company logo
- Default: Can use company logo

---

### Tab 3: Contact Information

**Phone** - Tel Input
- Label: "Phone"
- Validation: Optional, valid phone format
- Placeholder: "+966112345678"
- Input Mask: "+###-#########"

**Mobile** - Tel Input
- Label: "Mobile"
- Validation: Optional, valid phone format
- Placeholder: "+966501234567"
- Input Mask: "+###-#########"

**Fax** - Tel Input
- Label: "Fax"
- Validation: Optional, valid phone format
- Placeholder: "+966112345679"

**Email** - Email Input
- Label: "Email"
- Validation: Optional, valid email format
- Placeholder: "contact@company.com"

---

#### Form Actions (Bottom of Form)

**Create Company Button** (Primary)
- Text: "Create Company"
- Action: Submit form
- Loading state: Show spinner and disable button
- Validation: Validate all tabs before submit

**Cancel Button** (Secondary)
- Text: "Cancel"
- Action: Navigate back to companies list
- Confirmation: Show confirmation if form has changes

**Save as Draft Button** (Optional)
- Text: "Save Draft"
- Action: Save to local storage
- Feature: Auto-save every 30 seconds

---

### Form Validation

**Client-Side Validation**:
- Real-time validation on blur
- Show error messages below fields
- Highlight invalid fields in red
- Disable submit button if form invalid

**Server-Side Validation**:
- Display API validation errors
- Map errors to specific fields
- Show general errors at top of form

---

## Edit Company Screen

**Route**: `/companies/{id}/edit`  
**Access**: AdminOnly  
**Authorization**: AdminOnly policy

### API Integration

**Get Company Data**: `GET /api/companies/{id}`

**Update Company**: `PUT /api/companies/{id}`

**Request Body**:
```json
{
  "companyNameAr": "شركة الاختبار المحدثة",
  "companyNameEn": "Test Company Updated",
  "companyCode": "TC001",
  "legalNameAr": "شركة الاختبار للتجارة المحدثة",
  "legalNameEn": "Test Company for Trading Updated",
  "taxNumber": "123456789012345",
  "countryId": 1,
  "currId": 1,
  "defaultBranchId": 5,
  "companyLogoBase64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA..."
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Company updated successfully with logo",
  "data": 1,
  "statusCode": 200
}
```

### UI Components

Same form as Create Company, but:
- Form is pre-populated with existing data
- Company Code field is disabled (cannot be changed)
- Page title: "Edit Company"
- Submit button text: "Update Company"
- Show "Last Updated" information at top
- Add "View Details" link to navigate to details page

**Additional Features**:
- **Change History**: Show audit log of changes (optional)
- **Revert Changes**: Button to reset form to original values
- **Delete Company**: Button in header (with confirmation)

---

## Company Details Screen

**Route**: `/companies/{id}`  
**Access**: Authenticated users  
**Authorization**: Authenticated policy

### API Integration
**Endpoint**: `GET /api/companies/{id}`

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Company retrieved successfully with logo",
  "data": {
    "companyId": 1,
    "companyNameAr": "شركة الاختبار",
    "companyNameEn": "Test Company",
    "companyCode": "TC001",
    "legalNameAr": "شركة الاختبار للتجارة",
    "legalNameEn": "Test Company for Trading",
    "taxNumber": "123456789012345",
    "countryId": 1,
    "countryName": "Saudi Arabia",
    "currId": 1,
    "currencyCode": "SAR",
    "currencyName": "Saudi Riyal",
    "defaultBranchId": 5,
    "defaultBranchName": "Main Branch",
    "companyLogoBase64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...",
    "isActive": true,
    "createdAt": "2024-01-01T00:00:00Z",
    "createdBy": "superadmin",
    "updatedAt": "2024-01-15T10:30:00Z",
    "updatedBy": "admin"
  },
  "statusCode": 200
}
```

### UI Layout

#### Header Section
- **Company Logo**: Large display (150x150px)
- **Company Name**: Large, prominent (English/Arabic toggle)
- **Status Badge**: Active/Inactive
- **Action Buttons** (AdminOnly):
  - Edit Company
  - Delete Company
  - Deactivate/Activate

#### Details Section (Card Layout)

**Basic Information Card**:
- Company Name (Arabic): [value]
- Company Name (English): [value]
- Company Code: [value] (with copy button)
- Legal Name (Arabic): [value]
- Legal Name (English): [value]
- Tax Number: [value] (formatted, with copy button)

**Configuration Card**:
- Country: [country name] (with flag icon)
- Base Currency: [currency code - currency name]
- Default Branch: [branch name] (with link to branch details)
- Status: [Active/Inactive badge]

**System Information Card**:
- Created At: [formatted date]
- Created By: [username]
- Updated At: [formatted date]
- Updated By: [username]

#### Related Data Section (Tabs)

**Branches Tab**:
- List of all branches for this company
- Table with: Branch Name, Phone, Email, Status, Actions
- "Create Branch" button (AdminOnly)
- Click row to view branch details

**Users Tab**:
- List of all users across all branches
- Table with: Name, Username, Branch, Role, Status, Actions
- Filter by branch
- "Create User" button (AdminOnly)

**Statistics Tab**:
- Total Branches: [count]
- Total Users: [count]
- Active Users: [count]
- Recent Activity: Timeline of recent changes

---

## Logo Management

### Upload Logo

**Component**: File Upload with Preview

**Features**:
1. **Drag & Drop**:
   - Drag image file over upload area
   - Visual feedback on drag over
   - Drop to upload

2. **Click to Browse**:
   - Click upload area to open file picker
   - Filter: .png, .jpg, .jpeg files only

3. **Image Preview**:
   - Show thumbnail after upload
   - Display file name and size
   - Remove button to clear upload

4. **Validation**:
   - Max file size: 2MB
   - Allowed formats: PNG, JPG, JPEG
   - Recommended dimensions: 200x200px
   - Show error messages for invalid files

5. **Base64 Conversion**:
   - Automatically convert to Base64
   - Include data URI prefix: `data:image/png;base64,`
   - Store in form field for API submission

### Display Logo

**Component**: Image Display

**Features**:
1. **Thumbnail View** (List/Table):
   - Size: 40x40px
   - Rounded corners
   - Fallback to placeholder icon if no logo

2. **Large View** (Details Page):
   - Size: 150x150px
   - High quality display
   - Fallback to placeholder icon if no logo

3. **Placeholder**:
   - Show building icon when no logo
   - Gray background
   - Same dimensions as logo

### Update Logo

**Process**:
1. Click "Change Logo" button
2. Upload new image (same as upload process)
3. Preview new logo
4. Submit form to update
5. API receives new Base64 string
6. Old logo is replaced

### Remove Logo

**Process**:
1. Click "Remove Logo" button
2. Show confirmation dialog
3. On confirm, set `companyLogoBase64` to null
4. Submit update to API
5. Display placeholder icon

---

## Delete Company

**API Endpoint**: `DELETE /api/companies/{id}`

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Company deleted successfully",
  "data": 1,
  "statusCode": 200
}
```

### UI Flow:
1. User clicks Delete button
2. Show confirmation dialog:
   - Title: "Delete Company?"
   - Message: "Are you sure you want to delete [Company Name]? This will also affect all branches and users. This action cannot be undone."
   - Warning: Show count of affected branches and users
   - Checkbox: "I understand this action cannot be undone"
   - Buttons: "Delete" (danger, disabled until checkbox checked), "Cancel"
3. On confirm, call API
4. On success:
   - Show success message
   - Navigate to companies list
5. On error:
   - Show error message
   - Keep dialog open

---

## Next Steps

Continue to:
- [Part 4: Branch Management](./UI_API_GUIDE_PART4_BRANCH.md)
- [Part 5: User Management](./UI_API_GUIDE_PART5_USERS.md)
- [Part 6: Roles & Permissions](./UI_API_GUIDE_PART6_ROLES.md)
