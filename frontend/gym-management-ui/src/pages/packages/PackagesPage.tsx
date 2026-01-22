import { Container, Typography, Box, Button, Grid, Card, CardContent, CardActions, Chip } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { packageService } from '@services/packageService';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';

const PackagesPage = () => {
  const { data: packages, isLoading } = useQuery({
    queryKey: ['packages'],
    queryFn: () => packageService.getPackages(true),
  });

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Membership Packages</Typography>
        <Button variant="contained" startIcon={<AddIcon />}>
          Add Package
        </Button>
      </Box>

      {isLoading ? (
        <Typography>Loading...</Typography>
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
                    Duration: {pkg.durationDays} days
                  </Typography>
                  <Typography variant="body2">{pkg.description}</Typography>
                </CardContent>
                <CardActions>
                  <Button size="small" startIcon={<EditIcon />}>
                    Edit
                  </Button>
                  <Button size="small" color="error" startIcon={<DeleteIcon />}>
                    Delete
                  </Button>
                </CardActions>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}
    </Container>
  );
};

export default PackagesPage;
