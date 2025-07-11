using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Bearpro.AutoCrapper
{
    public class ResolutionContext { }

    public interface ITypeConverter<TSrc, TDst>
    {
        TDst Convert(TSrc source, TDst destination, ResolutionContext context);
    }

    class PropertyMap
    {
        public List<MemberInfo> SourcePath = new List<MemberInfo>();
        public List<MemberInfo> DestinationPath = new List<MemberInfo>();
        public Func<object, bool>? Condition;
        public bool Ignore;
        public bool UseDestinationValue;

        public PropertyMap Reverse()
        {
            return new PropertyMap
            {
                SourcePath = new List<MemberInfo>(DestinationPath),
                DestinationPath = new List<MemberInfo>(SourcePath),
                Ignore = this.Ignore,
                UseDestinationValue = this.UseDestinationValue
            };
        }
    }

    class MapConfig
    {
        public Type SrcType = null!;
        public Type DstType = null!;
        public Func<object, object>? Constructor;
        public List<PropertyMap> PropertyMaps = new List<PropertyMap>();
        public bool IgnoreUnmapped;
        public Action<object, object>? AfterMap;
        public object? Converter;
        public Profile Profile = null!;
    }

    static class ExpressionHelper
    {
        public static List<MemberInfo> GetPath(LambdaExpression expr)
        {
            var members = new List<MemberInfo>();
            Expression? body = expr.Body;
            if (body is UnaryExpression u && u.NodeType == ExpressionType.Convert)
                body = u.Operand;
            while (body is MemberExpression m)
            {
                if (m.Member is PropertyInfo || m.Member is FieldInfo)
                {
                    members.Insert(0, m.Member);
                    body = m.Expression;
                }
                else
                {
                    throw new InvalidOperationException("Only field or property access is supported");
                }
            }
            return members;
        }
    }

    static class TypeHelpers
    {
        public static Type GetElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType()!;
            return collectionType.GetGenericArguments().First();
        }

        public static bool IsEnumerableType(Type t)
        {
            return typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string);
        }
    }

    static class MemberHelpers
    {
        internal static Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo pi => pi.PropertyType,
                FieldInfo fi => fi.FieldType,
                _ => throw new InvalidOperationException()
            };
        }

        internal static object? GetMemberValue(MemberInfo member, object obj)
        {
            return member switch
            {
                PropertyInfo pi => pi.GetValue(obj),
                FieldInfo fi => fi.GetValue(obj),
                _ => throw new InvalidOperationException()
            };
        }

        internal static void SetMemberValue(MemberInfo member, object obj, object? value)
        {
            switch (member)
            {
                case PropertyInfo pi:
                    pi.SetValue(obj, value);
                    break;
                case FieldInfo fi:
                    fi.SetValue(obj, value);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    public interface IMappingExpression<TSrc, TDst>
    {
        IMappingExpression<TSrc, TDst> ForMember<TDstMember>(Expression<Func<TDst, TDstMember>> destinationMemberSelector, Action<DestinationMemberOptions<TSrc>> memberOptionSelector);
        IMappingExpression<TSrc, TDst> ForPath<TDstChildMember>(Expression<Func<TDst, TDstChildMember>> destinationMemberSelector, Action<DestinationMemberOptions<TSrc>> memberOptionSelector);
        IMappingExpression<TDst, TSrc> ReverseMap();
        IMappingExpression<TSrc, TDst> ConstructUsing(Func<object, TDst> constructor);
        IMappingExpression<TSrc, TDst> ForAllOtherMembers(Action<MultipleMemberOptions<TSrc>> memberOptionSelector);
        IMappingExpression<TSrc, TDst> AfterMap(Action<TSrc, TDst> afterMapAction);
        IMappingExpression<TSrc, TDst> ConvertUsing<TConverter>() where TConverter : ITypeConverter<TSrc, TDst>, new();
    }

    public class DestinationMemberOptions<TSrc>
    {
        private readonly PropertyMap _map;
        internal DestinationMemberOptions(PropertyMap map) { _map = map; }
        public void MapFrom<TSrcMember>(Expression<Func<TSrc, TSrcMember>> valueSelector) => _map.SourcePath = ExpressionHelper.GetPath(valueSelector);
        public void Condition(Func<TSrc, bool> condition) => _map.Condition = s => condition((TSrc)s);
        public void UseDestinationValue() => _map.UseDestinationValue = true;
        public void Ignore() => _map.Ignore = true;
    }

    public class MultipleMemberOptions<TSrc>
    {
        private readonly MapConfig _config;
        internal MultipleMemberOptions(MapConfig config) { _config = config; }
        public void Ignore() => _config.IgnoreUnmapped = true;
    }

    internal class MappingExpression<TSrc, TDst> : IMappingExpression<TSrc, TDst>
    {
        private readonly MapConfig _config;
        internal MappingExpression(MapConfig config) { _config = config; }

        public IMappingExpression<TSrc, TDst> ForMember<TDstMember>(Expression<Func<TDst, TDstMember>> destinationMemberSelector, Action<DestinationMemberOptions<TSrc>> memberOptionSelector)
        {
            var map = new PropertyMap { DestinationPath = ExpressionHelper.GetPath(destinationMemberSelector) };
            var opts = new DestinationMemberOptions<TSrc>(map);
            memberOptionSelector(opts);
            if (map.SourcePath.Count == 0)
            {
                var last = map.DestinationPath.Last();
                var sp = (MemberInfo?)typeof(TSrc).GetProperty(last.Name) ?? typeof(TSrc).GetField(last.Name);
                if (sp != null) map.SourcePath.Add(sp);
            }
            _config.PropertyMaps.Add(map);
            return this;
        }

        public IMappingExpression<TSrc, TDst> ForPath<TDstChildMember>(Expression<Func<TDst, TDstChildMember>> destinationMemberSelector, Action<DestinationMemberOptions<TSrc>> memberOptionSelector)
        {
            return ForMember(destinationMemberSelector, memberOptionSelector);
        }

        public IMappingExpression<TDst, TSrc> ReverseMap()
        {
            var rev = new MapConfig
            {
                SrcType = typeof(TDst),
                DstType = typeof(TSrc),
                Profile = _config.Profile,
                IgnoreUnmapped = _config.IgnoreUnmapped
            };
            foreach (var pm in _config.PropertyMaps)
            {
                rev.PropertyMaps.Add(pm.Reverse());
            }
            _config.Profile.Maps.Add(rev);
            return new MappingExpression<TDst, TSrc>(rev);
        }

        public IMappingExpression<TSrc, TDst> ConstructUsing(Func<object, TDst> constructor)
        {
            _config.Constructor = s => constructor(s);
            return this;
        }

        public IMappingExpression<TSrc, TDst> ForAllOtherMembers(Action<MultipleMemberOptions<TSrc>> memberOptionSelector)
        {
            var opts = new MultipleMemberOptions<TSrc>(_config);
            memberOptionSelector(opts);
            return this;
        }

        public IMappingExpression<TSrc, TDst> AfterMap(Action<TSrc, TDst> afterMapAction)
        {
            _config.AfterMap = (s, d) => afterMapAction((TSrc)s, (TDst)d);
            return this;
        }

        public IMappingExpression<TSrc, TDst> ConvertUsing<TConverter>() where TConverter : ITypeConverter<TSrc, TDst>, new()
        {
            _config.Converter = new TConverter();
            return this;
        }
    }

    public class Profile
    {
        internal List<MapConfig> Maps = new List<MapConfig>();
        public bool AllowNullCollections { get; set; } = false;

        protected IMappingExpression<TSrc, TDst> CreateMap<TSrc, TDst>()
        {
            var cfg = new MapConfig
            {
                SrcType = typeof(TSrc),
                DstType = typeof(TDst),
                Profile = this
            };
            Maps.Add(cfg);
            return new MappingExpression<TSrc, TDst>(cfg);
        }

        protected IMappingExpression<object, object> CreateMap(Type tSrc, Type tDst)
        {
            var cfg = new MapConfig { SrcType = tSrc, DstType = tDst, Profile = this };
            Maps.Add(cfg);
            return new MappingExpression<object, object>(cfg);
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
            internal List<Profile> Profiles = new List<Profile>();
            public void AddProfile(Profile profile) => Profiles.Add(profile);
            public void AddProfiles(IEnumerable<Profile> profiles) => Profiles.AddRange(profiles);
            public void AddProfile<TProfile>() where TProfile : Profile, new() => Profiles.Add(new TProfile());
        }

        internal ConfigurationProvider Provider;

        public MapperConfiguration(Action<MapperConfigurationOptions> configurationAction)
        {
            var opts = new MapperConfigurationOptions();
            configurationAction(opts);
            Provider = new ConfigurationProvider(opts.Profiles);
        }

        public Mapper CreateMapper() => new Mapper(this);
    }

    public class ConfigurationProvider
    {
        internal readonly Dictionary<(Type, Type), MapConfig> Maps = new Dictionary<(Type, Type), MapConfig>();

        public ConfigurationProvider(IEnumerable<Profile> profiles)
        {
            foreach (var p in profiles)
            {
                foreach (var map in p.Maps)
                {
                    map.Profile = p;
                    Seal(map);
                    Maps[(map.SrcType, map.DstType)] = map;
                }
            }
        }

        private void Seal(MapConfig map)
        {
            foreach (var prop in map.DstType.GetProperties().Where(x => x.CanWrite).Cast<MemberInfo>()
                .Concat(map.DstType.GetFields().Where(f => !f.IsInitOnly)))
            {
                if (map.PropertyMaps.Any(pm => pm.DestinationPath.Last() == prop))
                    continue;
                if (map.IgnoreUnmapped)
                    continue;
                MemberInfo? sp = map.SrcType.GetProperty(prop.Name) as MemberInfo ?? map.SrcType.GetField(prop.Name);
                if (sp == null)
                    continue;

                var dstType = MemberHelpers.GetMemberType(prop);
                var srcType = MemberHelpers.GetMemberType(sp);

                if (dstType.IsAssignableFrom(srcType))
                {
                    map.PropertyMaps.Add(new PropertyMap
                    {
                        DestinationPath = new List<MemberInfo> { prop },
                        SourcePath = new List<MemberInfo> { sp }
                    });
                    continue;
                }

                if (TypeHelpers.IsEnumerableType(dstType) && TypeHelpers.IsEnumerableType(srcType))
                {
                    var dstElem = TypeHelpers.GetElementType(dstType);
                    var srcElem = TypeHelpers.GetElementType(srcType);
                    if (dstElem.IsAssignableFrom(srcElem))
                    {
                        map.PropertyMaps.Add(new PropertyMap
                        {
                            DestinationPath = new List<MemberInfo> { prop },
                            SourcePath = new List<MemberInfo> { sp }
                        });
                    }
                }
            }
        }

        internal MapConfig? GetMap(Type src, Type dst)
        {
            Maps.TryGetValue((src, dst), out var m);
            return m;
        }
    }

    public class Mapper : IMapper
    {
        private readonly ConfigurationProvider _provider;
        public Mapper(MapperConfiguration config)
        {
            _provider = config.Provider;
        }

        public TDst Map<TDst>(object source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return (TDst)MapInternal(source, source.GetType(), typeof(TDst), null);
        }

        public TDst Map<TSrc, TDst>(TSrc source)
        {
            return (TDst)MapInternal(source!, typeof(TSrc), typeof(TDst), null);
        }

        public TDst Map<TDst>(IEnumerable<object> source)
        {
            var listType = typeof(TDst);
            var elemType = TypeHelpers.GetElementType(listType);
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
            foreach (var item in source)
            {
                list.Add(MapInternal(item, item.GetType(), elemType, null));
            }
            if (listType.IsArray)
                return (TDst)(object)list.Cast<object>().ToArray();
            return (TDst)list;
        }

        public TDst Map<TSrc, TDst>(TSrc source, TDst targetInstance)
        {
            return (TDst)MapInternal(source!, typeof(TSrc), typeof(TDst), targetInstance!);
        }

        public ConfigurationProvider ConfigurationProvider => _provider;

        private object MapInternal(object source, Type srcType, Type dstType, object? existing)
        {
            var map = _provider.GetMap(srcType, dstType);
            if (map == null)
                throw new NotImplementedException();
            if (map.Converter != null)
            {
                var method = map.Converter.GetType().GetMethod("Convert");
                return method!.Invoke(map.Converter, new[] { source, existing ?? Activator.CreateInstance(dstType)!, new ResolutionContext() })!;
            }
            object dest = existing ?? (map.Constructor != null ? map.Constructor(source) : Activator.CreateInstance(dstType)!);
            foreach (var pm in map.PropertyMaps)
            {
                if (pm.Ignore) continue;
                if (pm.UseDestinationValue && existing != null) continue;
                if (pm.Condition != null && !pm.Condition(source)) continue;
                object? value = GetValueFromPath(source, pm.SourcePath);
                SetValueToPath(dest, pm.DestinationPath, value, map.Profile.AllowNullCollections);
            }
            map.AfterMap?.Invoke(source, dest);
            return dest;
        }

        private static object? GetValueFromPath(object obj, List<MemberInfo> path)
        {
            object? current = obj;
            foreach (var p in path)
            {
                if (current == null) return null;
                current = MemberHelpers.GetMemberValue(p, current);
            }
            return current;
        }

        private static void SetValueToPath(object obj, List<MemberInfo> path, object? value, bool allowNullCollections)
        {
            object current = obj;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var p = path[i];
                var next = MemberHelpers.GetMemberValue(p, current);
                if (next == null)
                {
                    var t = MemberHelpers.GetMemberType(p);
                    next = Activator.CreateInstance(t)!;
                    MemberHelpers.SetMemberValue(p, current, next);
                }
                current = next;
            }
            var last = path.Last();

            if (value == null)
            {
                var lastType = MemberHelpers.GetMemberType(last);
                if (typeof(IEnumerable).IsAssignableFrom(lastType) && lastType != typeof(string))
                {
                    if (allowNullCollections)
                    {
                        MemberHelpers.SetMemberValue(last, current, null);
                    }
                    else
                    {
                        var elem = TypeHelpers.GetElementType(lastType);
                        if (lastType.IsArray)
                            MemberHelpers.SetMemberValue(last, current, Array.CreateInstance(elem, 0));
                        else
                            MemberHelpers.SetMemberValue(last, current, Activator.CreateInstance(typeof(List<>).MakeGenericType(elem)));
                    }
                    return;
                }
                MemberHelpers.SetMemberValue(last, current, null);
                return;
            }

            var lastPropType = MemberHelpers.GetMemberType(last);
            if (typeof(IEnumerable).IsAssignableFrom(lastPropType) && lastPropType != typeof(string) && value is IEnumerable srcEnum && !(value is string))
            {
                var elemType = TypeHelpers.GetElementType(lastPropType);
                IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                foreach (var item in srcEnum)
                {
                    list.Add(item);
                }
                if (lastPropType.IsArray)
                {
                    var arr = Array.CreateInstance(elemType, list.Count);
                    list.CopyTo(arr, 0);
                    MemberHelpers.SetMemberValue(last, current, arr);
                }
                else
                {
                    MemberHelpers.SetMemberValue(last, current, list);
                }
                return;
            }
            MemberHelpers.SetMemberValue(last, current, value);
        }
    }
}
