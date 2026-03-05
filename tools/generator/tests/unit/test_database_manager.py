import os
from unittest.mock import Mock, patch

import pytest

from orchestration.database_manager import DatabaseManager

class TestDatabaseManagerCleanup:

    def test_query_based_cleanup_queries_schema(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [
            ("public", "users"),
            ("public", "restaurants"),
            ("system", "config"),
        ]

        manager = DatabaseManager(mock_db)
        manager._cleanup_query_based()

        assert mock_db.fetch_all.called
        query = mock_db.fetch_all.call_args[0][0]
        assert "pg_tables" in query
        assert "schemaname" in query

    def test_query_based_cleanup_truncates_all_tables(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [
            ("public", "users"),
            ("public", "restaurants"),
            ("system", "config"),
        ]

        manager = DatabaseManager(mock_db)
        manager._cleanup_query_based()

        execute_calls = mock_db.execute_query.call_args_list
        truncate_calls = [call for call in execute_calls if "TRUNCATE" in str(call)]
        assert len(truncate_calls) == 3

    def test_query_based_cleanup_disables_fk_checks(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]

        manager = DatabaseManager(mock_db)
        manager._cleanup_query_based()

        execute_calls = mock_db.execute_query.call_args_list
        assert any("session_replication_role" in str(call) and "replica" in str(call) for call in execute_calls)

    def test_query_based_cleanup_restores_fk_checks(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]

        manager = DatabaseManager(mock_db)
        manager._cleanup_query_based()

        execute_calls = mock_db.execute_query.call_args_list
        assert any("session_replication_role" in str(call) and "origin" in str(call) for call in execute_calls)

    def test_query_based_cleanup_restores_fk_on_error(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]
        mock_db.execute_query.side_effect = [
            None,
            Exception("TRUNCATE failed"),
            None,
        ]

        manager = DatabaseManager(mock_db)

        with pytest.raises(Exception, match="TRUNCATE failed"):
            manager._cleanup_query_based()

        execute_calls = mock_db.execute_query.call_args_list
        assert len(execute_calls) == 3
        assert "origin" in str(execute_calls[2])

class TestDatabaseManager:

    def test_init_requires_only_db(self):
        mock_db = Mock()
        manager = DatabaseManager(mock_db)
        assert manager.db is mock_db

    @patch("builtins.print")
    @patch("builtins.input", return_value="yes")
    def test_cleanup_prompts_confirmation(self, mock_input, mock_print):
        mock_db = Mock()
        mock_db.fetch_all.return_value = []
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=False, auto_confirm=False)

        assert mock_print.called
        print_output = " ".join(str(call) for call in mock_print.call_args_list)
        assert "WARNING" in print_output or "delete ALL data" in print_output

        assert mock_input.called

    @patch("builtins.print")
    @patch("builtins.input", return_value="no")
    def test_cleanup_exits_on_no(self, mock_input, mock_print):
        mock_db = Mock()
        manager = DatabaseManager(mock_db)

        with pytest.raises(SystemExit):
            manager.cleanup(confirm=False, auto_confirm=False)

        assert not mock_db.fetch_all.called

    @patch.dict(os.environ, {"AUTO_CONFIRM_CLEANUP": "true"})
    @patch("builtins.print")
    def test_cleanup_auto_confirms_via_env_var(self, mock_print):
        mock_db = Mock()
        mock_db.fetch_all.return_value = []
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=False, auto_confirm=True)

        assert mock_db.fetch_all.called

    def test_cleanup_skips_prompt_if_confirm_true(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = []
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=True)

        assert mock_db.fetch_all.called

    def test_cleanup_uses_query_based_by_default(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=True)

        assert mock_db.fetch_all.called
        query = mock_db.fetch_all.call_args[0][0]
        assert "pg_tables" in query

    def test_cleanup_falls_back_to_cascade_on_error(self):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]
        call_count = 0

        def side_effect(query, *args):
            nonlocal call_count
            call_count += 1
            if "replica" in str(query):
                raise Exception("query_based failed")

        mock_db.execute_query.side_effect = side_effect
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=True)

        truncate_calls = [c for c in mock_db.execute_query.call_args_list if "TRUNCATE" in str(c)]
        assert len(truncate_calls) > 0

    def test_print_statistics_queries_table_counts(self):
        mock_db = Mock()
        mock_db.fetch_val.return_value = 100
        manager = DatabaseManager(mock_db)

        manager.print_statistics()

        assert mock_db.fetch_val.called
        assert mock_db.fetch_val.call_count >= 5

    def test_print_statistics_handles_tables(self):
        mock_db = Mock()
        mock_db.fetch_val.return_value = 42
        manager = DatabaseManager(mock_db)

        manager.print_statistics()

        queries = [call[0][0] for call in mock_db.fetch_val.call_args_list]
        assert any("users" in q for q in queries)
        assert any("restaurants" in q for q in queries)
        assert any("dishes" in q for q in queries)

class TestDatabaseManagerIntegration:

    @patch("builtins.print")
    @patch("builtins.input", return_value="yes")
    def test_full_cleanup_workflow(self, mock_input, mock_print):
        mock_db = Mock()
        mock_db.fetch_all.return_value = [
            ("public", "users"),
            ("public", "restaurants"),
            ("system", "config"),
        ]
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=False, auto_confirm=False)

        assert mock_print.called

        assert mock_input.called

        assert mock_db.fetch_all.called

        execute_calls = mock_db.execute_query.call_args_list
        assert any("replica" in str(call) for call in execute_calls)

        truncate_calls = [call for call in execute_calls if "TRUNCATE" in str(call)]
        assert len(truncate_calls) == 3

        assert any("origin" in str(call) for call in execute_calls)
