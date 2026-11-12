import com.sun.source.tree.*;
import com.sun.source.util.*;
import javax.lang.model.element.Modifier;
import javax.tools.*;
import java.nio.file.*;
import java.util.*;

/** Parse source only: no Forge dependencies or locally generated Arena schemas required. */
@SuppressWarnings("removal")
public class ControllerInventory {
    private static String json(String text) {
        return "\"" + text.replace("\\", "\\\\").replace("\"", "\\\"")
                .replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t") + "\"";
    }
    private static String type(Tree tree) {
        // Source-level matching only: normalize qualified/imported spellings, retain generic arguments and arrays.
        return tree.toString().replaceAll("(?:[A-Za-z_$][\\w$]*\\.)+([A-Za-z_$][\\w$]*)", "$1")
                .replaceAll("\\s+", "");
    }
    public static void main(String[] args) throws Exception {
        JavaCompiler compiler = ToolProvider.getSystemJavaCompiler();
        if (compiler == null) throw new IllegalStateException("Run with a JDK, not a JRE");
        DiagnosticCollector<JavaFileObject> diagnostics = new DiagnosticCollector<>();
        List<String> rows = new ArrayList<>();
        try (StandardJavaFileManager files = compiler.getStandardFileManager(diagnostics, null, null)) {
            JavacTask task = (JavacTask) compiler.getTask(null, files, diagnostics,
                    List.of("--release", "17", "-proc:none"), null, files.getJavaFileObjectsFromStrings(List.of(args)));
            Trees trees = Trees.instance(task);
            for (CompilationUnitTree unit : task.parse()) {
                String source = Files.readString(Path.of(unit.getSourceFile().toUri()));
                new TreeScanner<Void, String>() {
                    @Override public Void visitClass(ClassTree cls, String parent) {
                        String name = cls.getSimpleName().toString();
                        if (name.isEmpty()) return null; // anonymous classes do not declare controller hooks
                        String qualified = parent == null ? name : parent + "." + name;
                        for (Tree member : cls.getMembers()) {
                            if (member instanceof ClassTree) scan(member, qualified);
                            if (!(member instanceof MethodTree method) || method.getReturnType() == null) continue;
                            Set<Modifier> modifiers = method.getModifiers().getFlags();
                            if ((!modifiers.contains(Modifier.PUBLIC) && !modifiers.contains(Modifier.PROTECTED))
                                    || modifiers.contains(Modifier.STATIC)) continue;
                            List<String> params = new ArrayList<>();
                            for (VariableTree param : method.getParameters()) params.add(type(param.getType()));
                            String key = method.getName() + "(" + String.join(",", params) + ")";
                            long start = trees.getSourcePositions().getStartPosition(unit, method);
                            long end = trees.getSourcePositions().getEndPosition(unit, method);
                            List<String> superCalls = new ArrayList<>();
                            if (method.getBody() != null) new TreeScanner<Void, Void>() {
                                @Override public Void visitMethodInvocation(MethodInvocationTree call, Void unused) {
                                    String select = call.getMethodSelect().toString();
                                    if (select.startsWith("super.")) superCalls.add(select.substring(6));
                                    return super.visitMethodInvocation(call, unused);
                                }
                            }.scan(method.getBody(), null);
                            rows.add("{\"class\":" + json(qualified) + ",\"key\":" + json(key)
                                    + ",\"name\":" + json(method.getName().toString())
                                    + ",\"line\":" + unit.getLineMap().getLineNumber(start)
                                    + ",\"file\":" + json(Path.of(unit.getSourceFile().toUri()).toString())
                                    + ",\"abstract\":" + modifiers.contains(Modifier.ABSTRACT)
                                    + ",\"final\":" + modifiers.contains(Modifier.FINAL)
                                    + ",\"super_calls\":[" + String.join(",", superCalls.stream().distinct().map(ControllerInventory::json).toList()) + "]"
                                    + ",\"source\":" + json(source.substring((int)start, (int)end)) + "}");
                        }
                        return null;
                    }
                }.scan(unit, null);
            }
        }
        for (Diagnostic<?> d : diagnostics.getDiagnostics()) {
            if (d.getKind() == Diagnostic.Kind.ERROR) throw new IllegalArgumentException(d.toString());
        }
        System.out.println("[" + String.join(",\n", rows) + "]");
    }
}
