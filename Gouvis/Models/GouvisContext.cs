using Microsoft.EntityFrameworkCore;

namespace GDetailsApi.Gouvis.Models{
    public class GouvisContext: DbContext{
        public GouvisContext(DbContextOptions<GouvisContext> options): base(options){

        }

        public DbSet<GouvisDetails> GouvisDetailsDBSet {get; set;}
        
    }
}