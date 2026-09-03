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
  ListSubheader,
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
  TrendingUp as TrendingUpIcon,
} from '@mui/icons-material';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { useAuthStore } from '@store/authStore';
import { MemberSearch } from './MemberSearch';
import { useGymName } from '@lib/useGymName';
import { GYM, gymTint } from '@/config/gym';

const drawerWidth = 240;

export const AdminLayout = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [accountAnchor, setAccountAnchor] = useState<null | HTMLElement>(null);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuthStore();
  const gym = useGymName();

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

  // Which screens this person actually has. Reception is not shown the owner's, because a
  // menu full of items that bounce you back is worse than a shorter menu.
  //
  // The endpoints behind each of these enforce the same split independently - this list
  // decides what is worth showing, not what is allowed.
  const isAdmin = useAuthStore((state) => state.isAdmin)();

  // Grouped by what the person is doing, not by which table it reads.
  //
  // The flat list had Daily Takings, Who Owes Money and Payments sitting between Clients
  // and Packages, so finding anything meant reading all ten labels. Four short groups mean
  // the answer to "where do I look" is usually the heading, not the item.
  //
  // Empty groups are dropped rather than rendered as a bare heading, which is what keeps
  // reception's menu from ending in a "Setup" label with nothing under it.
  const menuGroups = [
    {
      heading: 'Today',
      items: [{ text: 'Today', icon: <DashboardIcon />, path: '/admin/today' }],
    },
    {
      heading: 'Members',
      items: [
        { text: 'All members', icon: <PeopleIcon />, path: '/admin/clients' },
        ...(isAdmin
          ? [{ text: 'Import members', icon: <UploadFileIcon />, path: '/admin/clients/import' }]
          : []),
      ],
    },
    {
      heading: 'Money',
      items: [
        { text: 'Payments', icon: <PaymentIcon />, path: '/admin/payments' },
        { text: 'Daily takings', icon: <ReceiptIcon />, path: '/admin/reports/daily-takings' },
        { text: 'Who owes money', icon: <MoneyOffIcon />, path: '/admin/reports/who-owes' },
        ...(isAdmin
          ? [{ text: 'Revenue', icon: <TrendingUpIcon />, path: '/admin/reports/revenue' }]
          : []),
      ],
    },
    {
      heading: 'Setup',
      items: isAdmin
        ? [
            { text: 'Packages', icon: <PackageIcon />, path: '/admin/packages' },
            { text: 'History', icon: <HistoryIcon />, path: '/admin/reports/history' },
            { text: 'Who can sign in', icon: <ManageAccountsIcon />, path: '/admin/users' },
            { text: 'Settings', icon: <SettingsIcon />, path: '/admin/settings' },
          ]
        : [],
    },
  ].filter((group) => group.items.length > 0);


  const drawer = (
    <Box>
      <Toolbar sx={{ bgcolor: GYM.colour.dark }}>
        <Typography variant="h6" noWrap sx={{ color: 'white', fontWeight: 'bold' }}>
          {gym.label}
        </Typography>
      </Toolbar>
      {menuGroups.map((group) => (
        <List
          key={group.heading}
          dense
          subheader={
            <ListSubheader
              disableSticky
              sx={{
                bgcolor: 'transparent',
                lineHeight: 2.2,
                fontSize: '0.7rem',
                letterSpacing: '0.09em',
                textTransform: 'uppercase',
              }}
            >
              {group.heading}
            </ListSubheader>
          }
        >
          {group.items.map((item) => (
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
                    backgroundColor: gymTint(0.12),
                    borderRight: `4px solid ${GYM.colour.main}`,
                    '&:hover': {
                      backgroundColor: gymTint(0.2),
                    },
                  },
                }}
              >
                <ListItemIcon
                  sx={{ color: location.pathname === item.path ? GYM.colour.main : 'inherit' }}
                >
                  {item.icon}
                </ListItemIcon>
                <ListItemText primary={item.text} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      ))}
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
            {gym.label}
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
