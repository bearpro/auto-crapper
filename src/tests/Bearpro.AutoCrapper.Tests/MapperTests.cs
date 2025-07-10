using FluentAssertions;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


namespace Bearpro.AutoCrapper.Tests
{
    public class MapperTests
    {
        // 1. Basic property mapping: source.Id => dest.Id, source.Name => dest.Name
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
            var dest = mapper.Map<Destination>(src);

            // Assert
            dest.Identifier.Should().Be(1);
            dest.FullName.Should().Be("Alice");
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

            var src = new Source { Name = "abc" };
            var result = mapper.Map<string>(src);

            result.Should().Be("<<abc>>");
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
            var dest = mapper.Map<DestinationFull>(src);

            dest.Id.Should().Be(1);
            dest.Name.Should().Be("A");
            dest.Extra.Should().BeNull();  // ignored
        }

        // 11. Collection mapping & AllowNullCollections
        [Fact]
        public void Should_Map_Collections_And_Preserve_Null_By_Default()
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
            dest2.Items.Should().BeNull();  // default behavior
        }

        [Fact]
        public void Should_Not_Preserve_Null_Collections_When_AllowNullCollections_True()
        {
            var config = new MapperConfiguration(opts =>
            {
                var p = new CollectionProfile { AllowNullCollections = true };
                opts.AddProfile(p);
            });
            var mapper = config.CreateMapper();

            var srcNull = new SourceWithList { Items = null };
            var dest = mapper.Map<DestinationWithList>(srcNull);
            dest.Items.Should().NotBeNull();
            dest.Items.Should().BeEmpty();
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

    public class DestinationFull
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Extra { get; set; }
    }

    // converter target
    public class SpecialDestination : Destination { public bool WasConstructed { get; set; } = true; }

    // collection
    public class SourceWithList { public string[] Items { get; set; } }
    public class DestinationWithList { public List<string> Items { get; set; } }
    // nested
    public class SourceNested { public string ChildName { get; set; } }
    public class ParentDestination { public ChildDest Child { get; set; } }
    public class ChildDest { public string Name { get; set; } }

    // Now: Profiles that wire up the mapping expressions in exactly the ways the tests expect.

    public class SimpleProfile : Profile
    {
        public SimpleProfile()
        {
            CreateMap<Source, Destination>();
        }
    }

    public class ReverseProfile : Profile
    {
        public ReverseProfile()
        {
            CreateMap<A, B>().ReverseMap();
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
                .ConstructUsing(src => new SpecialDestination());
        }
    }

    public class AfterMapProfile : Profile
    {
        public AfterMapProfile()
        {
            CreateMap<Source, Destination>()
                .AfterMap((s, d) => d.AfterCalled = true);
        }
    }

    public class ConvertUsingProfile : Profile
    {
        public ConvertUsingProfile()
        {
            CreateMap<Source, string>()
                .ConvertUsing<SurroundConverter>();
        }
    }
    public class SurroundConverter : ITypeConverter<Source, string>
    {
        public string Convert(Source source, string dest, ResolutionContext ctx)
            => $"<<{source.Name}>>";
    }

    public class AllOthersProfile : Profile
    {
        public AllOthersProfile()
        {
            CreateMap<Source, DestinationFull>()
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
}
