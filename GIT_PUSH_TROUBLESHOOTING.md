# Git Push Troubleshooting Guide

## Common Reasons Why Git Push Fails

### 1. **Authentication Issues**

#### Problem: Credentials not configured or expired
**Symptoms:**
- "Authentication failed"
- "Permission denied"
- "Could not read from remote repository"

**Solutions:**

**Option A: Use Personal Access Token (Recommended for GitHub/GitLab)**
```bash
# Configure Git to use credential manager
git config --global credential.helper manager-core

# When you push, you'll be prompted for credentials
# Username: your-username
# Password: your-personal-access-token (NOT your password)
```

**Option B: Configure SSH Key**
```bash
# Generate SSH key (if you don't have one)
ssh-keygen -t ed25519 -C "your-email@example.com"

# Add SSH key to ssh-agent
ssh-add ~/.ssh/id_ed25519

# Copy public key and add to GitHub/GitLab
cat ~/.ssh/id_ed25519.pub
```

**Option C: Update Remote URL**
```bash
# Check current remote
git remote -v

# Change to HTTPS (if using SSH and having issues)
git remote set-url origin https://github.com/username/repo.git

# Or change to SSH (if using HTTPS and having issues)
git remote set-url origin git@github.com:username/repo.git
```

---

### 2. **Remote Has Changes You Don't Have**

#### Problem: Remote branch has commits you don't have locally
**Symptoms:**
- "Updates were rejected"
- "Failed to push some refs"
- "Tip of your current branch is behind"

**Solution:**
```bash
# Pull the latest changes first
git pull origin main --rebase

# Or if you want to merge instead of rebase
git pull origin main

# Then push
git push origin main
```

---

### 3. **Large Files**

#### Problem: Files exceed GitHub's 100MB limit
**Symptoms:**
- "File is too large"
- "Remote rejected"
- Push hangs or fails

**Solution:**

**Check for large files:**
```bash
# Find files larger than 50MB
find . -type f -size +50M

# Check what's being pushed
git ls-files -s | awk '{if ($4 > 50000000) print $4, $2}'
```

**Remove large files from history:**
```bash
# Remove a specific large file
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch path/to/large/file" \
  --prune-empty --tag-name-filter cat -- --all

# Force push (WARNING: This rewrites history)
git push origin --force --all
```

**Use Git LFS for large files:**
```bash
# Install Git LFS
git lfs install

# Track large files
git lfs track "*.zip"
git lfs track "*.dll"

# Add .gitattributes
git add .gitattributes

# Commit and push
git commit -m "Add Git LFS tracking"
git push
```

---

### 4. **Branch Protection Rules**

#### Problem: Branch is protected and requires pull request
**Symptoms:**
- "Protected branch update failed"
- "Required status checks must pass"

**Solution:**
```bash
# Create a new branch
git checkout -b feature/your-feature-name

# Push to the new branch
git push origin feature/your-feature-name

# Then create a Pull Request on GitHub/GitLab
```

---

### 5. **Network Issues**

#### Problem: Connection timeout or network error
**Symptoms:**
- "Connection timed out"
- "Could not resolve host"
- "Failed to connect"

**Solution:**
```bash
# Test connection
ping github.com

# Try with verbose output
GIT_CURL_VERBOSE=1 git push origin main

# Increase buffer size
git config --global http.postBuffer 524288000

# Try with different protocol
git config --global url."https://".insteadOf git://
```

---

### 6. **Uncommitted Changes**

#### Problem: You have uncommitted changes
**Symptoms:**
- "You have unstaged changes"
- "Please commit your changes"

**Solution:**
```bash
# Check status
git status

# Stage all changes
git add .

# Commit
git commit -m "Your commit message"

# Push
git push origin main
```

---

### 7. **Wrong Branch**

#### Problem: Pushing to wrong branch or branch doesn't exist on remote
**Symptoms:**
- "src refspec main does not match any"
- "Failed to push some refs"

**Solution:**
```bash
# Check current branch
git branch

# Check remote branches
git branch -r

# Push and set upstream
git push -u origin main

# Or push to a different branch
git push origin your-branch-name
```

---

## Step-by-Step Troubleshooting

### Step 1: Check Git Status
```bash
git status
```

### Step 2: Check Remote Configuration
```bash
git remote -v
```

### Step 3: Check Current Branch
```bash
git branch
```

### Step 4: Try to Pull First
```bash
git pull origin main
```

### Step 5: Try to Push with Verbose Output
```bash
git push origin main --verbose
```

---

## Quick Fixes for Visual Studio

### Method 1: Use Visual Studio Git Integration

1. **Open Team Explorer** (View → Team Explorer)
2. **Go to Sync** section
3. **Click "Pull"** to get latest changes
4. **Click "Push"** to push your changes
5. If prompted, enter your credentials

### Method 2: Use Git Changes Window

1. **Open Git Changes** (View → Git Changes)
2. **Stage your changes** (click + icon)
3. **Enter commit message**
4. **Click "Commit All"**
5. **Click "Push"** button

### Method 3: Use Command Palette

1. Press **Ctrl+Q** to open Quick Launch
2. Type "Git: Push"
3. Select the command
4. Follow prompts

---

## Common Visual Studio Git Issues

### Issue: "Failed to push to the remote repository"

**Solution 1: Re-authenticate**
```
1. Go to Tools → Options → Source Control → Git Global Settings
2. Click "Credential Helper" → Select "manager-core"
3. Try pushing again
```

**Solution 2: Clear Credentials**
```
1. Open Credential Manager (Windows)
2. Find git:https://github.com entries
3. Remove them
4. Try pushing again (you'll be prompted for credentials)
```

**Solution 3: Use Git Bash**
```bash
# Open Git Bash in your project directory
cd /d/ThinkOnErp

# Pull first
git pull origin main

# Push
git push origin main
```

---

## Specific Solutions for Your Project

### If You're Pushing to GitHub

```bash
# Make sure you're using a Personal Access Token
# Go to: GitHub → Settings → Developer settings → Personal access tokens
# Generate new token with 'repo' scope
# Use this token as your password when pushing
```

### If You're Pushing to GitLab

```bash
# Make sure you're using a Personal Access Token
# Go to: GitLab → User Settings → Access Tokens
# Generate new token with 'write_repository' scope
# Use this token as your password when pushing
```

### If You're Pushing to Azure DevOps

```bash
# Use Git Credential Manager
git config --global credential.helper manager-core

# Or use Personal Access Token
# Go to: Azure DevOps → User Settings → Personal Access Tokens
# Generate new token with 'Code (Read & Write)' scope
```

---

## Emergency Solution: Force Push (Use with Caution!)

⚠️ **WARNING:** This will overwrite remote history. Only use if you're sure!

```bash
# Force push (overwrites remote)
git push origin main --force

# Safer alternative: force with lease
git push origin main --force-with-lease
```

---

## Check Your Specific Error

To help diagnose your specific issue, run these commands and share the output:

```bash
# 1. Check Git version
git --version

# 2. Check remote configuration
git remote -v

# 3. Check current branch
git branch

# 4. Check status
git status

# 5. Try to push with verbose output
git push origin main --verbose 2>&1
```

---

## Prevention Tips

1. **Always pull before pushing**
   ```bash
   git pull origin main
   git push origin main
   ```

2. **Use feature branches**
   ```bash
   git checkout -b feature/my-feature
   git push origin feature/my-feature
   ```

3. **Commit regularly**
   ```bash
   git add .
   git commit -m "Descriptive message"
   git push
   ```

4. **Keep credentials updated**
   - Use Personal Access Tokens instead of passwords
   - Regenerate tokens before they expire

5. **Check .gitignore**
   - Make sure large files are ignored
   - Don't commit sensitive data

---

## Need More Help?

If none of these solutions work, please provide:

1. The exact error message you're seeing
2. Output of `git remote -v`
3. Output of `git status`
4. Your Git hosting service (GitHub, GitLab, Azure DevOps, etc.)
5. Whether you're using HTTPS or SSH

---

## Quick Reference

| Issue | Command |
|-------|---------|
| Pull latest changes | `git pull origin main` |
| Push changes | `git push origin main` |
| Force push | `git push origin main --force` |
| Set upstream | `git push -u origin main` |
| Check remote | `git remote -v` |
| Check status | `git status` |
| Check branch | `git branch` |
| Update credentials | `git config --global credential.helper manager-core` |

---

**Last Updated:** 2026-05-06  
**For:** ThinkOnERP Project
