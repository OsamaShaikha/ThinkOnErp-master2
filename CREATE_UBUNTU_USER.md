# Create a Non-Root User on Ubuntu

Quick guide for creating a regular user account when logged in as root.

---

## Step 1: Create New User

```bash
# Create user with home directory
adduser thinkonerp

# You'll be prompted to:
# 1. Set password (enter twice)
# 2. Full Name (optional - press Enter to skip)
# 3. Room Number (optional - press Enter to skip)
# 4. Work Phone (optional - press Enter to skip)
# 5. Home Phone (optional - press Enter to skip)
# 6. Other (optional - press Enter to skip)
# 7. Confirm information (Y)
```

**Example:**
```bash
root@server:~# adduser thinkonerp
Adding user `thinkonerp' ...
Adding new group `thinkonerp' (1001) ...
Adding new user `thinkonerp' (1001) with group `thinkonerp' ...
Creating home directory `/home/thinkonerp' ...
Copying files from `/etc/skel' ...
New password: ********
Retype new password: ********
passwd: password updated successfully
Changing the user information for thinkonerp
Enter the new value, or press ENTER for the default
        Full Name []: ThinkOnErp Admin
        Room Number []: 
        Work Phone []: 
        Home Phone []: 
        Other []: 
Is the information correct? [Y/n] Y
```

---

## Step 2: Add User to Sudo Group (Optional but Recommended)

Give the user administrative privileges:

```bash
usermod -aG sudo thinkonerp
```

This allows the user to run commands with `sudo`.

---

## Step 3: Verify User Creation

```bash
# Check user exists
id thinkonerp

# Output should show:
# uid=1001(thinkonerp) gid=1001(thinkonerp) groups=1001(thinkonerp),27(sudo)
```

---

## Step 4: Switch to New User

```bash
# Switch to the new user
su - thinkonerp

# You should now see:
# thinkonerp@server:~$
```

---

## Step 5: Test Sudo Access (if added to sudo group)

```bash
# Test sudo access
sudo whoami

# Enter the user's password when prompted
# Should output: root
```

---

## Alternative: Create User with One Command

```bash
# Create user with home directory and bash shell
useradd -m -s /bin/bash thinkonerp

# Set password
passwd thinkonerp

# Add to sudo group
usermod -aG sudo thinkonerp
```

---

## Quick Setup Script

Create and run this script as root:

```bash
cat > /tmp/create_user.sh << 'EOF'
#!/bin/bash

USERNAME="thinkonerp"
PASSWORD="YourSecurePassword123"

# Create user
useradd -m -s /bin/bash $USERNAME

# Set password
echo "$USERNAME:$PASSWORD" | chpasswd

# Add to sudo group
usermod -aG sudo $USERNAME

# Add to docker group (if docker is installed)
if getent group docker > /dev/null 2>&1; then
    usermod -aG docker $USERNAME
fi

echo "User $USERNAME created successfully!"
echo "Password: $PASSWORD"
echo "Please change the password after first login!"
EOF

chmod +x /tmp/create_user.sh
/tmp/create_user.sh
```

---

## Step 6: Configure SSH Access (if needed)

If you want to SSH directly as this user:

```bash
# Edit SSH config (as root)
nano /etc/ssh/sshd_config

# Ensure these lines are present and uncommented:
# PermitRootLogin no
# PasswordAuthentication yes

# Restart SSH service
systemctl restart sshd
```

---

## Step 7: Set Up User Environment

Switch to the new user and set up their environment:

```bash
# Switch to user
su - thinkonerp

# Create common directories
mkdir -p ~/projects
mkdir -p ~/backups
mkdir -p ~/logs

# Set up bash aliases (optional)
cat >> ~/.bashrc << 'EOF'

# Custom aliases
alias ll='ls -lah'
alias update='sudo apt update && sudo apt upgrade -y'
alias ports='sudo netstat -tulpn'
alias dps='docker ps'
alias dlog='docker logs'

# Oracle environment (add after Oracle installation)
# export ORACLE_HOME=/opt/oracle/product/21c/dbhomeXE
# export ORACLE_SID=XE
# export PATH=$ORACLE_HOME/bin:$PATH
EOF

# Reload bash configuration
source ~/.bashrc
```

---

## Common User Management Commands

### View All Users
```bash
cat /etc/passwd | grep /home
```

### Delete User
```bash
# Delete user but keep home directory
userdel thinkonerp

# Delete user and home directory
userdel -r thinkonerp
```

### Change User Password
```bash
# As root
passwd thinkonerp

# As the user themselves
passwd
```

### Lock/Unlock User
```bash
# Lock user account
usermod -L thinkonerp

# Unlock user account
usermod -U thinkonerp
```

### View User Groups
```bash
groups thinkonerp
```

### Add User to Additional Groups
```bash
# Add to docker group
usermod -aG docker thinkonerp

# Add to www-data group (for web servers)
usermod -aG www-data thinkonerp
```

---

## Security Best Practices

1. **Use Strong Passwords**
   ```bash
   # Generate random password
   openssl rand -base64 16
   ```

2. **Disable Root SSH Login**
   ```bash
   # Edit SSH config
   nano /etc/ssh/sshd_config
   
   # Set: PermitRootLogin no
   
   # Restart SSH
   systemctl restart sshd
   ```

3. **Set Up SSH Keys** (more secure than passwords)
   ```bash
   # On your local machine, generate SSH key
   ssh-keygen -t rsa -b 4096 -C "your_email@example.com"
   
   # Copy public key to server
   ssh-copy-id thinkonerp@your_server_ip
   ```

4. **Configure Firewall**
   ```bash
   # Enable UFW
   ufw enable
   
   # Allow SSH
   ufw allow 22/tcp
   
   # Allow Oracle
   ufw allow 1521/tcp
   
   # Allow API
   ufw allow 5000/tcp
   ```

---

## Troubleshooting

### Issue: "User already exists"
```bash
# Check if user exists
id thinkonerp

# If exists, delete and recreate
userdel -r thinkonerp
adduser thinkonerp
```

### Issue: "Permission denied" when using sudo
```bash
# Verify user is in sudo group
groups thinkonerp

# If not, add to sudo group
usermod -aG sudo thinkonerp

# User needs to log out and back in for changes to take effect
```

### Issue: Can't switch to user
```bash
# Check user's shell
grep thinkonerp /etc/passwd

# Should end with /bin/bash
# If not, set it:
usermod -s /bin/bash thinkonerp
```

---

## Quick Reference

```bash
# Create user
adduser username

# Add to sudo
usermod -aG sudo username

# Switch to user
su - username

# Delete user
userdel -r username

# Change password
passwd username

# View user info
id username
```

---

## Next Steps After Creating User

1. ✅ User created
2. ✅ Added to sudo group
3. 🔄 Switch to new user: `su - thinkonerp`
4. 🔄 Clone ThinkOnErp repository
5. 🔄 Install Oracle Database (as new user with sudo)
6. 🔄 Deploy ThinkOnErp API

---

**User Created Successfully!** 🎉

You can now log out of root and use your new user account for daily operations.

```bash
# Exit root session
exit

# Or switch to new user
su - thinkonerp
```
