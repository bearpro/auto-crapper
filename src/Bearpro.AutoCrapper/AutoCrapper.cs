using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Bearpro.AutoCrapper;

public class ResolutionContext { }

public interface ITypeConverter<TSrc, TDst>
{
    TDst Convert(TSrc source, TDst destination, ResolutionContext context);
}

public interface IMappingExpression<TSrc, TDst>
{
    IMappingExpression<TSrc, TDst> ForMember<TDstMember>(
        Expression<Func<TDst, TDstMember>> destinationMemberSelector,
        Action<DestinationMemberOptions<TSrc>> memberOptionSelector);        
    
    IMappingExpression<TSrc, TDst> ForPath<TDstChildMember>(
        Expression<Func<TDst, TDstChildMember>> destinationMemberSelector,
        Action<DestinationMemberOptions<TSrc>> memberOptionSelector);

    IMappingExpression<TDst, TSrc> ReverseMap();
    
    IMappingExpression<TSrc, TDst> ConstructUsing(Func<object, TDst> constructor);

    IMappingExpression<TSrc, TDst> ForAllOtherMembers(
        Action<MultipleMemberOptions<TSrc>> memberOptionSelector);
    
    IMappingExpression<TSrc, TDst> AfterMap(
        Action<TSrc, TDst> afterMapAction);

    IMappingExpression<TSrc, TDst> ConvertUsing<TConverter>() where TConverter : ITypeConverter<TSrc, TDst>, new();
}

public class DestinationMemberOptions<TSrc>
{ 
    public void MapFrom<TSrcMember>(Expression<Func<TSrc, TSrcMember>> valueSelector)
    {

    }

    public void Condition(Func<TSrc, bool> condition)
    {

    }


    public void UseDestinationValue()
    {

    }

    public void Ignore()
    {

    }
}    

public class MultipleMemberOptions<TSrc>
{ 
    public void Ignore()
    {

    }
}


public class Profile
{
    public bool AllowNullCollections { get; set; } = false;

    protected IMappingExpression<TSrc, TDst> CreateMap<TSrc, TDst>()
    {
        throw new NotImplementedException();
    }        
    
    protected IMappingExpression<object, object> CreateMap(Type tSrc, Type tDst)
    {
        throw new NotImplementedException();
    }
}

public interface IMapper
{
    TDst Map<TDst>(object source);
    TDst Map<TSrc, TDst>(TSrc source);
    TDst Map<TSrc, TDst>(TSrc source, TDst targetInstance);
    TDst Map<TDst>(IEnumerable<object> source);
    ConfigurationProvider ConfigurationProvider { get; }
}

public class MapperConfiguration
{
    public class MapperConfigurationOptions
    {
        public void AddProfile(Profile profile)
        {
            
        }

        public void AddProfiles(IEnumerable<Profile> profiles)
        {
            
        }

        public void AddProfile<TProfile>() where TProfile : Profile, new()
        {

        }
    }

    public MapperConfiguration(Action<MapperConfigurationOptions> configurationAction)
    {
        
    }

    public Mapper CreateMapper()
    {
        return new Mapper(this);
    }
}

public class ConfigurationProvider
{

}

public class Mapper : IMapper
{
    public Mapper(MapperConfiguration config)
    {
        
    }

    public TDst Map<TDst>(object source)
    {
        throw new NotImplementedException();
    }

    public TDst Map<TSrc, TDst>(TSrc source)
    {
        throw new NotImplementedException();
    }

    public TDst Map<TDst>(IEnumerable<object> source)
    {
        throw new NotImplementedException();
    }

    public TDst Map<TSrc, TDst>(TSrc source, TDst targetInstance)
    {
        throw new NotImplementedException();
    }

    public ConfigurationProvider ConfigurationProvider => throw new NotImplementedException();
}