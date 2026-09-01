import { useState } from 'react';
import {
  AppBar,
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Toolbar,
  Typography,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Dashboard as DashboardIcon,
  People as PeopleIcon,
  LocalOffer as PackageIcon,
  Payment as PaymentIcon,
  Settings as SettingsIcon,
  Logout as LogoutIcon,
  ArrowDropDown as ArrowDropDownIcon,
  Lock as LockIcon,
  UploadFile as UploadFileIcon,
  MoneyOff as MoneyOffIcon,
  ReceiptLong as ReceiptIcon,
  History as HistoryIcon,
  ManageAccounts as ManageAccountsIcon,
} from '@mui/icons-material';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { useAuthStore } from '@store/authStore';
import { MemberSearch } from './MemberSearch';

const drawerWidth = 240;

export const AdminLayout = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [accountAnchor, setAccountAnchor] = useState<null | HTMLElement>(null);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuthStore();

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  const handleLogout = () => {
    setAccountAnchor(null);
    logout();
    navigate('/login');
  };

  const goTo = (path: string) => {
    setAccountAnchor(null);
    navigate(path);
  };

  const menuItems = [
    { text: 'Dashboard', icon: <DashboardIcon />, path: '/admin/dashboard' },
    { text: 'Clients', icon: <PeopleIcon />, path: '/admin/clients' },
    { text: 'Import Members', icon: <UploadFileIcon />, path: '/admin/clients/import' },
    { text: 'Payments', icon: <PaymentIcon />, path: '/admin/payments' },
    { text: 'Daily Takings', icon: <ReceiptIcon />, path: '/admin/reports/daily-takings' },
    { text: 'Who Owes Money', icon: <MoneyOffIcon />, path: '/admin/reports/who-owes' },
    { text: 'Packages', icon: <PackageIcon />, path: '/admin/packages' },
    { text: 'History', icon: <HistoryIcon />, path: '/admin/reports/history' },
    { text: 'Who Can Sign In', icon: <ManageAccountsIcon />, path: '/admin/users' },
    { text: 'Settings', icon: <SettingsIcon />, path: '/admin/settings' },
  ];

  const drawer = (
    <Box>
      <Toolbar sx={{ bgcolor: '#1b5e20' }}>
        <Typography variant="h6" noWrap sx={{ color: 'white', fontWeight: 'bold' }}>
          🐻 Fit Bear Gym
        </Typography>
      </Toolbar>
      <List>
        {menuItems.map((item) => (
          <ListItem key={item.text} disablePadding>
            <ListItemButton
              selected={location.pathname === item.path}
              // Closes the drawer as well as navigating. On a phone the temporary drawer
              // covers the page it just opened, so leaving it up hides the result of the tap.
              onClick={() => {
                setMobileOpen(false);
                navigate(item.path);
              }}
              sx={{
                '&.Mui-selected': {
                  backgroundColor: 'rgba(46, 125, 50, 0.12)',
                  borderRight: '4px solid #2e7d32',
                  '&:hover': {
                    backgroundColor: 'rgba(46, 125, 50, 0.2)',
                  },
                },
              }}
            >
              <ListItemIcon sx={{ color: location.pathname === item.path ? '#2e7d32' : 'inherit' }}>
                {item.icon}
              </ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex' }}>
      {/* AppBar */}
      <AppBar
        position="fixed"
        sx={{
          width: { sm: `calc(100% - ${drawerWidth}px)` },
          ml: { sm: `${drawerWidth}px` },
        }}
      >
        <Toolbar>
          <IconButton
            color="inherit"
            edge="start"
            onClick={handleDrawerToggle}
            sx={{ mr: 2, display: { sm: 'none' } }}
          >
            <MenuIcon />
          </IconButton>
          {/* The gym name gives way to the search box on a phone. Reception needs to find
              a member far more often than it needs reminding which gym it is in. */}
          <Typography
            variant="h6"
            noWrap
            sx={{ mr: 2, display: { xs: 'none', md: 'block' } }}
          >
            🐻 The Fit Bear Gym
          </Typography>

          <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: { xs: 'flex-start', md: 'center' } }}>
            <MemberSearch />
          </Box>

          <Button
            color="inherit"
            endIcon={<ArrowDropDownIcon />}
            onClick={(e) => setAccountAnchor(e.currentTarget)}
            sx={{ textTransform: 'none', display: { xs: 'none', sm: 'inline-flex' } }}
          >
            {user?.fullName || 'Admin'}
          </Button>
          <IconButton
            color="inherit"
            aria-label="Account"
            onClick={(e) => setAccountAnchor(e.currentTarget)}
            sx={{ display: { xs: 'inline-flex', sm: 'none' } }}
          >
            <ArrowDropDownIcon />
          </IconButton>
          <Menu
            anchorEl={accountAnchor}
            open={Boolean(accountAnchor)}
            onClose={() => setAccountAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            transformOrigin={{ vertical: 'top', horizontal: 'right' }}
          >
            <MenuItem onClick={() => goTo('/admin/change-password')}>
              <ListItemIcon>
                <LockIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Change password</ListItemText>
            </MenuItem>
            <Divider />
            <MenuItem onClick={handleLogout}>
              <ListItemIcon>
                <LogoutIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Log out</ListItemText>
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      {/* Drawer */}
      <Box
        component="nav"
        sx={{ width: { sm: drawerWidth }, flexShrink: { sm: 0 } }}
      >
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={handleDrawerToggle}
          ModalProps={{ keepMounted: true }}
          sx={{
            display: { xs: 'block', sm: 'none' },
            '& .MuiDrawer-paper': { boxSizing: 'border-box', width: drawerWidth },
          }}
        >
          {drawer}
        </Drawer>
        <Drawer
          variant="permanent"
          sx={{
            display: { xs: 'none', sm: 'block' },
            '& .MuiDrawer-paper': { boxSizing: 'border-box', width: drawerWidth },
          }}
          open
        >
          {drawer}
        </Drawer>
      </Box>

      {/* Main Content */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          // Desktop padding wastes a third of a phone screen's width.
          p: { xs: 1, sm: 3 },
          width: { sm: `calc(100% - ${drawerWidth}px)` },
          minWidth: 0,
        }}
      >
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
};
