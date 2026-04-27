# Git Push Quick Fix Guide

## 🔴 Problem: Cannot Push to Remote Repository

## ✅ Quick Solutions (Try in Order)

### Solution 1: Pull First, Then Push
```bash
git pull origin main
git push origin main
```

### Solution 2: Check and Fix Authentication
```bash
# Configure credential helper
git config --global credential.helper manager-core

# Try pushing again
git push origin main
```

### Solution 3: Set Upstream Branch
```bash
# If branch doesn't exist on remote
git push -u origin main
```

### Solution 4: Use Visual Studio Git Integration
1. Open **Team Explorer** (View → Team Explorer)
2. Click **Sync**
3. Click **Pull** first
4. Then click **Push**

---

## 🔍 Diagnose Your Specific Issue

Run this PowerShell script to diagnose the problem:
```powershell
.\diagnose-git-push.ps1
```

This will check:
- ✓ Git installation
- ✓ Remote configuration
- ✓ Current branch
- ✓ Uncommitted changes
- ✓ Large files
- ✓ Network connectivity
- ✓ Credentials

---

## 📋 Most Common Issues

### Issue 1: Authentication Failed
**Error:** "Authentication failed" or "Permission denied"

**Fix:**
```bash
# Use Personal Access Token (not password)
# GitHub: Settings → Developer settings → Personal access tokens
# GitLab: User Settings → Access Tokens

# When prompted for password, use your token instead
```

### Issue 2: Remote Has Changes
**Error:** "Updates were rejected" or "Failed to push"

**Fix:**
```bash
git pull origin main --rebase
git push origin main
```

### Issue 3: Large Files
**Error:** "File is too large" or push hangs

**Fix:**
```bash
# Check for large files
Get-ChildItem -Recurse -File | Where-Object { $_.Length -gt 50MB }

# Add to .gitignore or use Git LFS
```

### Issue 4: No Remote Configured
**Error:** "No configured push destination"

**Fix:**
```bash
# Add remote repository
git remote add origin https://github.com/username/repo.git

# Verify
git remote -v

# Push
git push -u origin main
```

### Issue 5: Wrong Branch
**Error:** "src refspec main does not match any"

**Fix:**
```bash
# Check current branch
git branch

# Push to correct branch
git push origin your-branch-name
```

---

## 🚀 Step-by-Step Fix

### Step 1: Check Status
```bash
git status
```

### Step 2: Commit Changes (if any)
```bash
git add .
git commit -m "Your commit message"
```

### Step 3: Pull Latest Changes
```bash
git pull origin main
```

### Step 4: Push
```bash
git push origin main
```

---

## 🛠️ Tools Created for You

1. **diagnose-git-push.ps1** - Automated diagnostic tool
2. **GIT_PUSH_TROUBLESHOOTING.md** - Comprehensive troubleshooting guide
3. **GIT_PUSH_QUICK_FIX.md** - This quick reference (you are here)

---

## 💡 Pro Tips

1. **Always pull before pushing**
   ```bash
   git pull && git push
   ```

2. **Use verbose output to see what's happening**
   ```bash
   git push origin main --verbose
   ```

3. **Check your remote URL**
   ```bash
   git remote -v
   ```

4. **Use credential manager**
   ```bash
   git config --global credential.helper manager-core
   ```

---

## 🆘 Still Not Working?

1. Run the diagnostic script:
   ```powershell
   .\diagnose-git-push.ps1
   ```

2. Check the detailed guide:
   - Open `GIT_PUSH_TROUBLESHOOTING.md`

3. Share the error message:
   - Copy the exact error from the terminal
   - Include output of `git remote -v`
   - Include output of `git status`

---

## 📞 Quick Commands Reference

| Task | Command |
|------|---------|
| Check status | `git status` |
| Check remote | `git remote -v` |
| Pull changes | `git pull origin main` |
| Push changes | `git push origin main` |
| Set upstream | `git push -u origin main` |
| Force push | `git push origin main --force` |
| Verbose push | `git push origin main --verbose` |

---

**Created:** 2026-05-06  
**For:** ThinkOnERP Project  
**Priority:** High
