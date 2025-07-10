# Auto Crapper

A single file AutoMapper interface subset implementation.  
Key aim of this project is to prove that AutoMapper abstractions are not free, by
implementing simple reflection based runtime mapping without any precompilation, IL emission, and caching.

## Key features

- Simple map DTO's in runtime according to specified configuration.  
- Easy embeddeble. Just insert `AutoCrapper.cs` in your project.
- Zero startup time even when using thousands of
  profiles.

# Project structure

- `/src` - All source code and tests
- `/src/Bearpro.AutoCrapper` - Actual library sources
- `/src/Bearpro.AutoCrapper/AutoCrapper.cs` - Core implementation - mapping configurations and actual mapping
- `/src/Bearpro.AutoCrapper/AutoCrapper.QueryableExtensions.cs` - A little extension to project IQueryable using mapping configuration
- `/src/tests/Bearpro.AutoCrapper.Tests` - Tests for the library

# Contribution guide

- Feel free to implement any missing features, but remember to keep it simple and 
efficient. Do not overthink library that can be completely replaced by simple
`static B Map(A)`.

- Before adding new feature write corresponding test. If you chainging tests,
  make shure they matches original AutoMapper 10.1.1 behaviour.

- Try not to use modern language features. The origin of this project comes from
  attempt to speed up .Net 4.6.1 ASP.Net application startup, that 
  was originally dependent on AutoMapper.

- To run tests:
  ```
  cd src
  dotnet test
  ```
