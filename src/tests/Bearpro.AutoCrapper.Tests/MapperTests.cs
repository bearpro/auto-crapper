using FluentAssertions;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

// It is necessary check for AutoMapper compatibility, so do not forget to 
// run your tests both with AutoMapper and AutoCrapper every time you change
// any tests:
// - dotnet test '-p:DefineConstants=AUTOMAPPER' # to run with AutoMapper
// - dotnet test                                 # to run with AutoCrapper

#if AUTOMAPPER
using AutoMapper;
#else
using Bearpro.AutoCrapper;
#endif

namespace Tests
{
    public class MapperTests
    {
        // 1. Basic property mapping with profile: source.Id => dest.Id, source.Name => dest.Name
        [Fact]
        public void Should_Map_Simple_Properties()
        {
            // Arrange
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<SimpleProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Id = 1, Name = "Alice" };

            // Act
            var dest = mapper.Map<DestinationMatchingSource>(src);

            // Assert
            dest.Id.Should().Be(1);
            dest.Name.Should().Be("Alice");
        }

        // 1.1. Basic property mapping without profile should fail.
        [Fact]
        public void Should_Not_Map_Simple_Properties_Without_Profile()
        {
            // Arrange
            var mapper = new Mapper(new MapperConfiguration(_ => { }));

            var src = new Source { Id = 1, Name = "Alice" };

            // Act
            Assert.ThrowsAny<Exception>(() => { mapper.Map<Destination>(src); });
        }

        // 1.2. Basic collection mapping
        [Fact]
        public void Should_Map_Simple_Properties_InCollections()
        {
            // Arrange
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<SimpleProfile>();
            });
            var mapper = config.CreateMapper();

            List<Source> src = [new() { Id = 1, Name = "Alice" }];

            // Act
            var dest = mapper.Map<List<DestinationMatchingSource>>(src);

            // Assert
            dest.Should().HaveCount(1);
            dest[0].Id.Should().Be(1);
            dest[0].Name.Should().Be("Alice");
        }

        // 2. Reverse mapping
        [Fact]
        public void Should_ReverseMap_When_ReverseMap_Called()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<ReverseProfile>();
            });
            var mapper = config.CreateMapper();

            var original = new A { ValueA = "foo" };
            var b = mapper.Map<B>(original);

            // sanity check forward
            b.ValueB.Should().Be("foo");

            // now reverse
            var back = mapper.Map<A>(b);
            back.ValueA.Should().Be("foo");
        }

        // 3. ForMember(MapFrom)
        [Fact]
        public void Should_Map_Member_From_Different_Source()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<MemberProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Id = 5, Name = "Bob" };
            var dest = mapper.Map<Destination>(src);

            dest.Identifier.Should().Be(5);
            dest.FullName.Should().Be("Bob");
        }

        // 4. Condition
        [Fact]
        public void Should_Conditionally_Map_Only_When_Condition_Is_True()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<ConditionProfile>();
            });
            var mapper = config.CreateMapper();

            var good = new Source { Id = 42, Name = null };

            var dest1 = mapper.Map<Destination>(good);
            dest1.FullName.Should().BeNull();       // condition fails (Name is null)

            var alsoGood = new Source { Id = 43, Name = "OK" };
            var dest2 = mapper.Map<Destination>(alsoGood);
            dest2.FullName.Should().Be("OK");       // condition passes
        }

        // 5. Ignore
        [Fact]
        public void Should_Ignore_Member_When_Ignore_Called()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<IgnoreProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Id = 9, Name = "ShouldNotMap" };
            var dest = new Destination { FullName = "KeepThis" };
            mapper.Map(src, dest);

            dest.Identifier.Should().Be(0);             // default(int)
            dest.FullName.Should().Be("KeepThis");  // preserved
        }

        // 6. UseDestinationValue
        [Fact]
        public void Should_UseDestinationValue_When_Configured()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<UseDestValueProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Name = "NewName" };
            var dest = new Destination { FullName = "Existing" };
            mapper.Map(src, dest);

            dest.FullName.Should().Be("Existing");
        }

        // 7. ConstructUsing
        [Fact]
        public void Should_Construct_Destination_Using_Custom_Factory()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<ConstructProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Id = 7 };
            var dest = mapper.Map<Destination>(src);

            dest.Should().BeOfType<SpecialDestination>();
            ((SpecialDestination)dest).WasConstructed.Should().BeTrue();
            dest.Identifier.Should().Be(7);
        }

        // 8. AfterMap
        [Fact]
        public void Should_Invoke_AfterMap_Action()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<AfterMapProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Name = "x" };
            var dest = mapper.Map<Destination>(src);

            dest.FullName.Should().Be("x");
            dest.AfterCalled.Should().BeTrue();
        }

        // 9. ConvertUsing<TConverter>
        [Fact]
        public void Should_Convert_Using_Custom_TypeConverter()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<ConvertUsingProfile>();
            });
            var mapper = config.CreateMapper();

            var src = EnumA.A1;
            var result = mapper.Map<EnumB>(src);

            result.Should().Be(EnumB.B1);
        }

        // 10. ForAllOtherMembers.Ignore()
        [Fact]
        public void Should_Ignore_All_Other_Members_When_Configured()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<AllOthersProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Id = 1, Name = "A", Extra = "Z" };
            var dest = mapper.Map<DestinationMatchingSource>(src);

            dest.Id.Should().Be(1);
            dest.Name.Should().Be("A");
            dest.Extra.Should().BeNull();  // ignored
        }

        // 11. Collection mapping & AllowNullCollections
        [Fact]
        public void Should_Map_Collections_And_Replace_Null_With_Empty_By_Default()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<CollectionProfile>();
            });
            var mapper = config.CreateMapper();

            var srcWithList = new SourceWithList { Items = new[] { "a", "b" } };
            var dest1 = mapper.Map<DestinationWithList>(srcWithList);
            dest1.Items.Should().Equal("a", "b");

            var srcNull = new SourceWithList { Items = null };
            var dest2 = mapper.Map<DestinationWithList>(srcNull);
            dest2.Items.Should().BeEmpty();  // default behavior
        }

        [Fact]
        public void Should_Preserve_Null_Collections_When_AllowNullCollections_True()
        {
            var config = new MapperConfiguration(opts =>
            {
                var p = new CollectionProfile { AllowNullCollections = true };
                opts.AddProfile(p);
            });
            var mapper = config.CreateMapper();

            var srcNull = new SourceWithList { Items = null };
            var dest = mapper.Map<DestinationWithList>(srcNull);
            dest.Items.Should().BeNull();
        }

        // 12. ForPath (nested)
        [Fact]
        public void Should_Map_Nested_Path_With_ForPath()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<NestedProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new SourceNested { ChildName = "C" };
            var dest = mapper.Map<ParentDestination>(src);

            dest.Child.Should().NotBeNull();
            dest.Child.Name.Should().Be("C");
        }

        // 13. Field mapping
        [Fact]
        public void Should_Map_Fields()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<FieldsProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new SourceWithFields { id = 1, name = "FieldName" };
            var dest = mapper.Map<DestinationWithFields>(src);

            dest.id.Should().Be(1);
            dest.name.Should().Be("FieldName");
        }

        // 13.1. Field mapping to properties
        [Fact]
        public void Should_Map_Properties_From_Fields()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<FieldsProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new SourceWithFields { id = 1, name = "FieldName" };
            var dest = mapper.Map<DestinationMatchingSource>(src);

            dest.Id.Should().Be(1);
            dest.Name.Should().Be("FieldName");
        }

        // 13.2. Field mapping from properties
        [Fact]
        public void Should_Map_Fields_From_Properties()
        {
            var config = new MapperConfiguration(opts =>
            {
                opts.AddProfile<FieldsProfile>();
            });
            var mapper = config.CreateMapper();

            var src = new Source { Id = 1, Name = "FieldName" };
            var dest = mapper.Map<DestinationWithFields>(src);

            dest.id.Should().Be(1);
            dest.name.Should().Be("FieldName");
        }

        // Complex mapping
        [Fact]
        public void Should_Map_FootballTeam_To_FootballTeamDto()
        {
            var config = new MapperConfiguration(opts =>
            {
                // HACK We need to run Automapper 10.1.1 tests in modern runtime,
                // so by this we preventing bug between old AutoMapper and 
                // generic math
                #if AUTOMAPPER
                opts.ShouldMapMethod = _ => false;
                #endif

                opts.AddProfile<FootballTeamProfile>();
            });
            var mapper = config.CreateMapper();

            var team = new FootballTeam
            {
                CoachId = 10,
                SponsorId = 20,
                Players = new List<PlayerEntity>
                {
                    new PlayerEntity { PlayerRoleTypeId = 1, AccountId = 100 },
                    new PlayerEntity { PlayerRoleTypeId = 2, AccountId = 200 }
                }
            };

            var dto = mapper.Map<FootballTeamDto>(team);

            dto.Coach.Should().Be(10);
            dto.Sponsor.Should().Be(20);
            dto.Players.Should().HaveCount(2);
            dto.Players[0].Role.TypeId.Should().Be(1);
            dto.Players[0].Account.Id.Should().Be(100);
            dto.Players[1].Role.TypeId.Should().Be(2);
            dto.Players[1].Account.Id.Should().Be(200);
        }
    }

    // === SUPPORTING TYPES & PROFILES FOR THE TESTS ===

    // simple src/dest
    public class Source
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Extra { get; set; }
    }

    public class Destination
    {
        public int Identifier { get; set; }
        public string FullName { get; set; }

        // used in AfterMap
        public bool AfterCalled { get; set; }
    }

    public class DestinationMatchingSource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Extra { get; set; }
    }

    // converter target
    public class SpecialDestination : Destination
    {
        public bool WasConstructed { get; set; } = true;
    }

    // collection
    public class SourceWithList { public string[] Items { get; set; } }
    public class DestinationWithList { public List<string> Items { get; set; } }
    // nested
    public class SourceNested { public string ChildName { get; set; } }
    public class ParentDestination { public ChildDest Child { get; set; } }
    public class ChildDest { public string Name { get; set; } }

    public enum EnumA { A1, A2, A3 }
    public enum EnumB { None, B1, B2, B3 }

    // Now: Profiles that wire up the mapping expressions in exactly the ways the tests expect.

    public class SimpleProfile : Profile
    {
        public SimpleProfile()
        {
            CreateMap<Source, DestinationMatchingSource>();
        }
    }

    public class ReverseProfile : Profile
    {
        public ReverseProfile()
        {
            CreateMap<A, B>()
                .ForMember(dest => dest.ValueB, opt => opt.MapFrom(src => src.ValueA))
                .ReverseMap();
        }
    }

    public class MemberProfile : Profile
    {
        public MemberProfile()
        {
            CreateMap<Source, Destination>()
                .ForMember(d => d.Identifier, opt => opt.MapFrom(s => s.Id))
                .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.Name));
        }
    }

    public class ConditionProfile : Profile
    {
        public ConditionProfile()
        {
            CreateMap<Source, Destination>()
                .ForMember(d => d.FullName, opt =>
                {
                    opt.Condition(src => src.Name != null);
                    opt.MapFrom(src => src.Name);
                });
        }
    }

    public class IgnoreProfile : Profile
    {
        public IgnoreProfile()
        {
            CreateMap<Source, Destination>()
                .ForMember(d => d.Identifier, opt => opt.Ignore())
                .ForMember(d => d.FullName, opt => opt.Ignore());
        }
    }

    public class UseDestValueProfile : Profile
    {
        public UseDestValueProfile()
        {
            CreateMap<Source, Destination>()
                .ForMember(d => d.FullName, opt => opt.UseDestinationValue());
        }
    }

    public class ConstructProfile : Profile
    {
        public ConstructProfile()
        {
            CreateMap<Source, Destination>()
                .ConstructUsing(src => new SpecialDestination())
                .ForMember(d => d.Identifier, opt => opt.MapFrom(s => s.Id));
        }
    }

    public class AfterMapProfile : Profile
    {
        public AfterMapProfile()
        {
            CreateMap<Source, Destination>()
                .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.Name))
                .AfterMap((s, d) => d.AfterCalled = true);
        }
    }

    public class ConvertUsingProfile : Profile
    {
        public ConvertUsingProfile()
        {
            CreateMap<EnumA, EnumB>()
                .ConvertUsing<EnumConverter>();
        }
    }
    public class EnumConverter : ITypeConverter<EnumA, EnumB>
    {
        public EnumB Convert(EnumA source, EnumB destination, ResolutionContext context)
        {
            switch (source)
            {
                case EnumA.A1: return EnumB.B1;
                case EnumA.A2: return EnumB.B2;
                case EnumA.A3: return EnumB.B3;
                default: throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }
    }

    public class AllOthersProfile : Profile
    {
        public AllOthersProfile()
        {
            CreateMap<Source, DestinationMatchingSource>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
                .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name))
                .ForAllOtherMembers(opt => opt.Ignore());
        }
    }

    public class CollectionProfile : Profile
    {
        public CollectionProfile()
        {
            CreateMap<SourceWithList, DestinationWithList>();
        }
    }

    public class NestedProfile : Profile
    {
        public NestedProfile()
        {
            CreateMap<SourceNested, ParentDestination>()
                .ForPath(d => d.Child.Name, opt => opt.MapFrom(s => s.ChildName));
        }
    }

    // Dummy types for ReverseMap test
    public class A { public string ValueA { get; set; } }
    public class B { public string ValueB { get; set; } }

    // Types to test field mappings
    public class SourceWithFields
    {
        public int id;
        public string name;
    }

    public class DestinationWithFields
    {
        public int id;
        public string name;
    }

    public class FieldsProfile : Profile
    {
        public FieldsProfile()
        {
            CreateMap<SourceWithFields, DestinationWithFields>();
            CreateMap<SourceWithFields, DestinationMatchingSource>();
            CreateMap<Source, DestinationWithFields>();
        }
    }

    // Complex mapping data

    public class FootballTeam
    {
        public List<PlayerEntity> Players { get; set; }
        public int CoachId { get; set; }
        public int SponsorId { get; set; }
    }

    public class FootballTeamDto
    {
        public List<Player> Players { get; set; }
        public int Coach { get; set; }
        public int Sponsor { get; set; }
    }

    public class PlayerEntity
    {
        public int PlayerRoleTypeId { get; set; }
        public int AccountId { get; set; }
    }

    public class Player
    {
        public PlayerRole Role { get; set; }
        public PlayerAccount Account { get; set; }
    }

    public class PlayerRole
    {
        public int TypeId { get; set; }
    }

    public class PlayerAccount
    {
        public int Id { get; set; }
    }

    public class FootballTeamProfile : Profile
    {
        public FootballTeamProfile()
        {
            CreateMap<FootballTeam, FootballTeamDto>()
                .ForMember(
                    x => x.Players,
                    x => x.MapFrom(
                        y => y.Players.Select(z => new Player
                        {
                            Role = new PlayerRole
                            {
                                TypeId = z.PlayerRoleTypeId,
                            },
                            Account = new PlayerAccount
                            {
                                Id = z.AccountId
                            }
                        })))
                .ForMember(x => x.Coach, x => x.MapFrom(y => y.CoachId))
                .ForMember(x => x.Sponsor, x => x.MapFrom(y => y.SponsorId))
                .ReverseMap();
        }
    }
}
