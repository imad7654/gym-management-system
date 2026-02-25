import { useState } from 'react';
import {
  Container,
  Typography,
  Box,
  Button,
  Grid,
  Card,
  CardContent,
  CardActions,
  Chip,
  CircularProgress,
  Alert,
  Snackbar,
} from '@mui/material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { packageService } from '@services/packageService';
import type { Package } from '../../types';
import { PackageFormDialog, DeletePackageDialog } from '@components/packages';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import ToggleOnIcon from '@mui/icons-material/ToggleOn';
import ToggleOffIcon from '@mui/icons-material/ToggleOff';

const PackagesPage = () => {
  const queryClient = useQueryClient();
  const [openDialog, setOpenDialog] = useState(false);
  const [selectedPackage, setSelectedPackage] = useState<Package | null>(null);
  const [openDeleteDialog, setOpenDeleteDialog] = useState(false);
  const [packageToDelete, setPackageToDelete] = useState<Package | null>(null);
  const [showToggleSuccess, setShowToggleSuccess] = useState(false);
  const [toggleMessage, setToggleMessage] = useState('');

  const { data: packages, isLoading, isError } = useQuery({
    queryKey: ['packages'],
    queryFn: () => packageService.getPackages(true),
  });

  const toggleStatusMutation = useMutation({
    mutationFn: (id: number) => packageService.toggleStatus(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['packages'] });
      setShowToggleSuccess(true);
    },
  });

  const handleAddPackage = () => {
    setSelectedPackage(null);
    setOpenDialog(true);
  };

  const handleEditPackage = (pkg: Package) => {
    setSelectedPackage(pkg);
    setOpenDialog(true);
  };

  const handleToggleStatus = (pkg: Package) => {
    const newStatus = pkg.isActive ? 'deactivated' : 'activated';
    setToggleMessage(`Package ${newStatus} successfully!`);
    toggleStatusMutation.mutate(pkg.id);
  };

  const handleDeletePackage = (pkg: Package) => {
    setPackageToDelete(pkg);
    setOpenDeleteDialog(true);
  };

  const handleCloseDialog = () => {
    setOpenDialog(false);
    setSelectedPackage(null);
  };

  const handleCloseDeleteDialog = () => {
    setOpenDeleteDialog(false);
    setPackageToDelete(null);
  };

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Membership Packages</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={handleAddPackage}>
          Add Package
        </Button>
      </Box>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 400 }}>
          <CircularProgress />
        </Box>
      ) : isError ? (
        <Alert severity="error">Failed to load packages. Please try again.</Alert>
      ) : packages && packages.length === 0 ? (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <Typography variant="h6" color="text.secondary" gutterBottom>
            No packages found
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Get started by creating your first membership package
          </Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleAddPackage}>
            Add Package
          </Button>
        </Box>
      ) : (
        <Grid container spacing={3}>
          {packages?.map((pkg) => (
            <Grid item xs={12} sm={6} md={4} key={pkg.id}>
              <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
                <CardContent sx={{ flexGrow: 1 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                    <Typography variant="h5" component="h2">
                      {pkg.name}
                    </Typography>
                    <Chip
                      label={pkg.isActive ? 'Active' : 'Inactive'}
                      color={pkg.isActive ? 'success' : 'default'}
                      size="small"
                    />
                  </Box>
                  <Typography variant="h4" color="primary" gutterBottom>
                    ${pkg.price.toFixed(2)}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Duration: {pkg.durationDays} days ({Math.floor(pkg.durationDays / 30)} months)
                  </Typography>
                  <Typography variant="body2">{pkg.description || 'No description'}</Typography>
                </CardContent>
                <CardActions sx={{ justifyContent: 'space-between' }}>
                  <Box>
                    <Button size="small" startIcon={<EditIcon />} onClick={() => handleEditPackage(pkg)}>
                      Edit
                    </Button>
                    <Button
                      size="small"
                      color={pkg.isActive ? 'warning' : 'success'}
                      startIcon={pkg.isActive ? <ToggleOffIcon /> : <ToggleOnIcon />}
                      onClick={() => handleToggleStatus(pkg)}
                      disabled={toggleStatusMutation.isPending}
                    >
                      {pkg.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                  </Box>
                  <Button
                    size="small"
                    color="error"
                    startIcon={<DeleteIcon />}
                    onClick={() => handleDeletePackage(pkg)}
                  >
                    Delete
                  </Button>
                </CardActions>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <PackageFormDialog
        open={openDialog}
        onClose={handleCloseDialog}
        package={selectedPackage}
      />

      <DeletePackageDialog
        open={openDeleteDialog}
        onClose={handleCloseDeleteDialog}
        package={packageToDelete}
      />

      <Snackbar
        open={showToggleSuccess}
        autoHideDuration={3000}
        onClose={() => setShowToggleSuccess(false)}
        message={toggleMessage}
      />
    </Container>
  );
};

export default PackagesPage;
