# dot-env-generator
A source generator for C# that turns .env files into runtime constants. 

## Basic Usage

After adding the source generator to your project add an item group to your project that references your .env file like so:

```xml
  <ItemGroup>
    <AdditionalFiles Include="example.env" />
  </ItemGroup>
```

The contents of your .env file would look something like this:
```env
# A string literal
A_STRING="yep, that is a string."
# A string
ALSO_STRING=RANDOM_0026256698
# An array of string
STRING_ARRAY=one,two,three,four,five
# An array of numbers
INT_ARRAY=1, 2, 3, 4, 5, 6, 7
A_NUMBER=1000
A_BIGGER_NUMBER=10_000_000
A_GUID=de095b54-2082-40f8-a928-794da5675e7c
A_DATE_TIME=Tue, 1 Jan 2008 00:00:00Z
# A base64 encoded bytearray 
A_BYTE_ARRAY=d2hhdA==
A_DOUBLE=1.0
A_SCIENCE_DOUBLE=3.2e23
A_DOUBLE_LITERAL=1.0D
A_HEX=0xFF0000
A_UINT=2000U
A_LONG=2000L
A_ULONG=2000UL
A_NEGATIVE_LONG=-2000L
```

The source generator will use your .env file first as a lookup table. Using the left-hand declarations the generator will check for an environment variable with that name in descending order (machine, user, and finally the process.) 

If the variable does not exist in any of these stores, the value on the right is used. If not default value is defined, an error is reported.

The value parsing rules are simple:
- Anything wrapped in double quotes is treated as a string.
- If an array has multiple types the entire array is treated as an array of strings.
- Any value that cannot have its CLR type determined is treated as a string.
- Base64 strings are always considered to be byte arrays. 

## Known Issues 
- Some strings may be detected as being Base64 even if they aren't. If you encounter this issue just wrap you string in double quotes.
- No tokenzation or lexing is performed on the .env file, and as such there is no validation of the .env file or its values. The parsing is very rudimentary.
