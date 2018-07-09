# google-diff-match-patch-csharp

Evolution of the C# port of the google diff-match-patch implementation. 

Provides a simple object model to cope with diffs and patches. The main classes involved are `Diff` and `Patch`. Next to those, the static `DiffList` and `PatchList` classes provide some static and extension methods on `List<Diff>` and `List<Patch>`, respectively.

## Example usages

See also the unit tests but here are some typical scenarios:

    var text1 = "Lorem ipsum dolor sit amet, consectetuer adipiscing elit, \r\n" +
                "sed diam nonummy nibh euismod tincidunt ut laoreet dolore magna \r\n" +
                "sed diam nonummy nibh euismod tincidunt ut laoreet dolore magna \r\n" +
                "sed diam nonummy nibh euismod tincidunt ut laoreet dolore magna \r\n" +
                "aliquam erat volutpat. Ut wisi enim ad minim veniam, quis nostrud exerci \r\n" +
                "tation ullamcorper suscipit lobortis nisl ut aliquip ex ea commodo consequat. \r\n" +
                "Duis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie \r\n" +
                "consequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan\r\n" +
                "et iusto odio dignissim qui blandit praesent luptatum zzril delenit augue duis dolore \r\n" +
                "te feugait nulla facilisi. Nam liber tempor cum soluta nobis eleifend option congue nihil \r\n" +
                "imperdiet doming id quod mazim placerat facer possim assum. Typi non habent claritatem insitam; \r\n" +
                "est usus legentis in iis qui facit eorum claritatem. Investigationes demonstraverunt lectores \r\n" +
                "legere me lius quod ii legunt saepius. Claritas est etiam processus dynamicus, qui sequitur\r\n" +
                "mutationem consuetudium lectorum. Mirum est notare quam littera gothica, quam nunc putamus \r\n" +
                "parum claram, anteposuerit litterarum formas humanitatis per seacula quarta decima et quinta \r\n" +
                "decima. Eodem modo typi, qui nunc nobis videntur parum clari, fiant sollemnes in futurum.";

    var text2 = "Lorem ipsum dolor sit amet, adipiscing elit, \r\n" +
                "sed diam nonummy nibh euismod tincidunt ut laoreet dolore vobiscum magna \r\n" +
                "aliquam erat volutpat. Ut wisi enim ad minim veniam, quis nostrud exerci \r\n" +
                "tation ullamcorper suscipit lobortis nisl ut aliquip ex ea commodo consequat. \r\n" +
                "Duis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie \r\n" +
                "consequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan\r\n" +
                "et iusto odio dignissim qui blandit praesent luptatum zzril delenit augue duis dolore \r\n" +
                "te feugait nulla facilisi. Nam liber tempor cum soluta nobis eleifend option congue nihil \r\n" +
                "imperdiet doming id quod mazim placerat facer possim assum. Typi non habent claritatem insitam; \r\n" +
                "est usus legentis in iis qui facit eorum claritatem. Investigationes demonstraverunt lectores \r\n" +
                "legere me lius quod ii legunt saepius. Claritas est etiam processus dynamicus, qui sequitur\r\n" +
                "mutationem consuetudium lectorum. Mirum est notare quam littera gothica, putamus \r\n" +
                "parum claram, anteposuerit litterarum formas humanitatis per seacula quarta decima et quinta \r\n" +
                "decima. Eodem modo typi, qui nunc nobis videntur parum clari, fiant sollemnes in futurum.";

Computing a list of diffs from 2 strings:

    List<Diff> diffs = Diff.Compute(text1, text2);

Generating a list of patches from a list of diffs:

    List<Patch> patches = Patch.FromDiffs(diffs);

Extension method to convert a list of patch objects to a textual representation:

    var textualRepresentation = patches.ToText();

Parse a textual representation of patches and return a List of Patch objects:

    List<Patch> patches = PatchList.Parse(textualRepresentation);

Apply a list of patches to a source text:

    (string newText, bool[] results) = patches.Apply(text1);
    Debug.Assert(results.All(result => result == true));
    Debug.Assert(newText == text2);

Compute the source or destination text from a list of diffs:

    var text1 = diffs.ToText1();
    var text2 = diffs.ToText2();

Represent a list of diffs in a pretty html format:

    var html = diffs.PrettyHtml();

Transform a list of diffs into a string representation of the operations required to transform text1 into text2. E.g. `=4\t-1\t+ing` would transform 'skype' to 'skyping' (keep 4 chars, delete 1 char, insert 'ing'). Operations are tab-separated. Inserted text is escaped using %xx notation.

    var delta = diffs.ToDelta();

Given the source text and a string formatted in the 'delta' format, compute the full list of diffs:

    var diffs = DiffList.FromDelta(text1, delta);

