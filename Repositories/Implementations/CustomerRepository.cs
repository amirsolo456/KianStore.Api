using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Repositories.Interfaces;

namespace KianStore.Api.Repositories.Implementations;

public class CustomerRepository : ICustomerRepository
{
    private readonly KianStoreDbContext _context;

    public CustomerRepository(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<Taraf?> GetByMobileAsync(string mobile)
    {
        mobile = mobile.Trim();
        return await _context.Tarafs
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                !t.IsDisabled &&
                ((t.Mobile != null && t.Mobile == mobile) || (t.Phone != null && t.Phone == mobile)));
    }

    public async Task<Taraf?> GetByIdAsync(int id)
    {
        return await _context.Tarafs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.IdType == 2 && !t.IsDisabled);
    }

    public async Task<List<Taraf>> SearchAsync(string search, int page = 1, int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        search = search.Trim();

        var query = _context.Tarafs
            .AsNoTracking()
            .Where(t => !t.IsDisabled && t.IdType == 2);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pSearch = search.ToPersianChars();
            var aSearch = search.ToArabicChars();
            bool isNumeric = int.TryParse(search, out int idSearch);

            query = query.Where(t =>
                (t.Name != null && (t.Name.Contains(pSearch) || t.Name.Contains(aSearch))) ||
                (t.Mobile != null && t.Mobile.Contains(pSearch)) ||
                (t.Phone != null && t.Phone.Contains(pSearch)) ||
                (isNumeric && t.Id == idSearch));
        }

        return await query
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Taraf> CreateAsync(Taraf taraf)
    {
        // Taraf is a legacy table with many required columns and business defaults.
        // The existing AndAddTaraf procedure is the canonical creation path and
        // populates those fields exactly as the desktop KianStore application does.
        var name = taraf.Name.Trim();
        var phone = taraf.Phone?.Trim() ?? string.Empty;
        var mobile = taraf.Mobile?.Trim() ?? string.Empty;
        var address = taraf.Address?.Trim() ?? string.Empty;

        var nameParameter = new SqlParameter("@name", System.Data.SqlDbType.VarChar, 50) { Value = name };
        var tellParameter = new SqlParameter("@tell", System.Data.SqlDbType.VarChar, 50) { Value = phone };
        var mobileParameter = new SqlParameter("@mobile", System.Data.SqlDbType.VarChar, 70) { Value = mobile };
        var addressParameter = new SqlParameter("@addr", System.Data.SqlDbType.VarChar, 200) { Value = address };
        var gpsLatParameter = new SqlParameter("@GpsLat", System.Data.SqlDbType.Float) { Value = 0d };
        var gpsLongParameter = new SqlParameter("@GpsLong", System.Data.SqlDbType.Float) { Value = 0d };
        var newIdParameter = new SqlParameter("@newIDTaraf", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.InputOutput,
            Value = 0
        };
        var idMasirParameter = new SqlParameter("@IDMasir", System.Data.SqlDbType.Int) { Value = 0 };
        var codeMelliParameter = new SqlParameter("@CodeMelli", System.Data.SqlDbType.VarChar, 30) { Value = string.Empty };
        var codeEghtesadiParameter = new SqlParameter("@CodeEghtesadi", System.Data.SqlDbType.VarChar, 50) { Value = string.Empty };
        var idUserParameter = new SqlParameter("@IDUser", System.Data.SqlDbType.Int) { Value = 0 };

        await _context.Database.ExecuteSqlRawAsync(
            "EXEC dbo.AndAddTaraf @name, @tell, @mobile, @addr, @GpsLat, @GpsLong, @newIDTaraf OUTPUT, @IDMasir, @CodeMelli, @CodeEghtesadi, @IDUser",
            nameParameter,
            tellParameter,
            mobileParameter,
            addressParameter,
            gpsLatParameter,
            gpsLongParameter,
            newIdParameter,
            idMasirParameter,
            codeMelliParameter,
            codeEghtesadiParameter,
            idUserParameter);

        var newId = (int)newIdParameter.Value;
        var created = await _context.Tarafs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == newId && t.IdType == 2);

        if (created == null)
        {
            throw new InvalidOperationException("مشتری ایجاد شد اما رکورد ایجادشده از جدول Taraf قابل بازیابی نیست.");
        }

        return created;
    }

    public async Task<bool> ExistsByMobileAsync(string mobile)
    {
        mobile = mobile.Trim();
        return await _context.Tarafs.AnyAsync(t =>
            !t.IsDisabled &&
            t.IdType == 2 &&
            ((t.Mobile != null && t.Mobile == mobile) || (t.Phone != null && t.Phone == mobile)));
    }

    public async Task<int> GetNextIdAsync()
    {
        var maxId = await _context.Tarafs
            .Where(t => t.IdType == 2)
            .MaxAsync(t => (int?)t.Id) ?? 0;

        return maxId + 1;
    }
}
