-- Contagem exata (count(*), não estimativa) de cada tabela do schema public — inclui as tabelas do
-- Identity (AspNet*) e o __EFMigrationsHistory. Uma linha por tabela, "tabela<TAB>linhas", ordenada pelo
-- nome em ordem de bytes (COLLATE "C"), para que a saída da produção e a da restauração sejam
-- comparáveis byte a byte. Uma única instrução, sem meta-comandos do psql: pacote.sh a roda dentro da
-- transação que exportou o snapshot do pg_dump (contagens = dump), restaurar-pacote.sh com psql -At.
SELECT c.relname || E'\t' ||
       (xpath('/row/n/text()',
              query_to_xml(format('SELECT count(*) AS n FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
WHERE c.relkind IN ('r', 'p')
ORDER BY c.relname COLLATE "C";
